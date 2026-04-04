using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Connectors;
using Nexus.Application.Options;
using Nexus.Application.Repositories;
using Nexus.Application.Sync;
using Nexus.Connectors.Core.Abstractions;
using Nexus.Connectors.Core.Exceptions;
using Nexus.Core.ValueObjects;
using CoreExternalRef = Nexus.Core.ValueObjects.ExternalRef;
using CoreIntegration = Nexus.Core.Entities.Integration;
using CoreServiceItem = Nexus.Core.Entities.ServiceItem;
using CoreUnifiedTask = Nexus.Core.Entities.UnifiedTask;

namespace Nexus.Infrastructure.Sync;

/// <summary>
/// Service responsible for automatically synchronizing tasks from integrated external services.
/// </summary>
public sealed class TaskAutoSyncService(
    IIntegrationRepository integrationRepository,
    ITaskRepository taskRepository,
    IConnectorRegistry connectorRegistry,
    IConnectorPluginCatalog connectorPluginCatalog,
    IOptions<SyncOptions> syncOptions,
    ILogger<TaskAutoSyncService> logger) : ITaskAutoSyncService
{
    private readonly IIntegrationRepository _integrationRepository = integrationRepository;
    private readonly ITaskRepository _taskRepository = taskRepository;
    private readonly IConnectorRegistry _connectorRegistry = connectorRegistry;
    private readonly IConnectorPluginCatalog _connectorPluginCatalog = connectorPluginCatalog;
    private readonly IOptions<SyncOptions> _syncOptions = syncOptions;
    private readonly ILogger<TaskAutoSyncService> _logger = logger;

    /// <inheritdoc/>
    public async Task SyncAllAsync(CancellationToken ct = default)
    {
        var options = _syncOptions.Value;
        if (!options.AutoSyncEnabled)
        {
            return;
        }

        foreach (var serviceType in GetAutoSyncServiceTypes())
        {
            ct.ThrowIfCancellationRequested();
            var integrations = await _integrationRepository.GetByServiceTypeAsync(serviceType, ct);
            foreach (var integration in integrations.Where(i => i.IsEnabled))
            {
                ct.ThrowIfCancellationRequested();
                await SyncIntegrationAsync(integration, options.DefaultLookbackDays, ct);
            }
        }
    }

    private async Task SyncIntegrationAsync(CoreIntegration integration, int lookbackDays, CancellationToken ct)
    {
        var serviceType = integration.ServiceType;

        var connector = _connectorRegistry.Get(serviceType);
        if (connector is null)
        {
            _logger.LogWarning("Auto-sync skipped for {ServiceType}: connector not registered.", serviceType);
            return;
        }

        if (connector.ShouldSkipAutoSyncFor(integration))
        {
            return;
        }

        try
        {
            var since = integration.LastSyncAt ?? DateTimeOffset.UtcNow.AddDays(-lookbackDays);
            var coreIntegration = ToCoreIntegration(integration);
            var items = await connector.FetchItemsAsync(coreIntegration, since, ct);
            await UpsertTasksAsync(connector, integration, items, ct);

            integration.RecordSync();
            await _integrationRepository.UpdateAsync(integration, ct);

            _logger.LogInformation("Auto-sync completed for {ServiceType}. {ItemCount} item(s) processed.", serviceType, items.Count);
        }
        catch (ReconnectRequiredException ex)
        {
            _logger.LogWarning(ex, "Auto-sync skipped for {ServiceType}: reconnect required.", ex.ServiceType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-sync failed for {ServiceType}.", serviceType);
        }
    }

    private async Task UpsertTasksAsync(
        IServiceConnector connector,
        CoreIntegration integration,
        IReadOnlyList<CoreServiceItem> items,
        CancellationToken ct)
    {
        var syncedAt = DateTimeOffset.UtcNow;
        var serviceType = integration.ServiceType;

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            var mappedTask = connector.MapToUnifiedTask(item, integrationId: integration.Id);
            var existing = await _taskRepository.GetByExternalRefAsync(serviceType, item.ExternalId, integration.Id, ct);

            if (existing is null)
            {
                var newTask = ToApplicationTask(mappedTask);
                newTask.MarkSynced(syncedAt);
                await _taskRepository.AddAsync(newTask, ct);
                continue;
            }

            existing.Update(
                title: mappedTask.Title,
                description: mappedTask.Description,
                status: mappedTask.Status,
                dueAt: mappedTask.DueAt,
                createdAt: mappedTask.CreatedAt);

            if (mappedTask.Priority is not null)
            {
                existing.SetPriority(mappedTask.Priority.Value);
            }

            if (mappedTask.ExternalRef is not null)
            {
                existing.SetExternalRef(ToApplicationExternalRef(mappedTask.ExternalRef));
            }

            existing.MarkSynced(syncedAt);
            await _taskRepository.UpdateAsync(existing, ct);
        }
    }

    private IEnumerable<ServiceType> GetAutoSyncServiceTypes()
        => _connectorPluginCatalog
            .GetAll()
            .Where(m => m.SyncProfile.SupportsAutoSync)
            .Select(m => m.ServiceType);

    private CoreIntegration ToCoreIntegration(CoreIntegration integration)
    {
        var core = new CoreIntegration(
            integration.ServiceType,
            integration.DisplayName,
            integration.AuthMode,
            integration.IsEnabled,
            integration.Id);
        core.SetConfigJson(integration.ConfigJson);
        core.SetTokenJson(integration.TokenJson);
        return core;
    }

    private CoreUnifiedTask ToApplicationTask(CoreUnifiedTask task)
    {
        var appTask = new CoreUnifiedTask(
            task.Title,
            task.Description,
            task.Status,
            task.Priority,
            task.SyncFromSource,
            task.DueAt,
            task.ExternalRef is null ? null : ToApplicationExternalRef(task.ExternalRef),
            task.CreatedAt,
            task.ProjectId);
        return appTask;
    }

    private CoreExternalRef ToApplicationExternalRef(CoreExternalRef externalRef)
        => new(
            externalRef.ServiceType,
            externalRef.ExternalId,
            externalRef.Url,
            externalRef.ProjectKey,
            externalRef.IntegrationId);
}
