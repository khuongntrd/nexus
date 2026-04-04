using Microsoft.AspNetCore.Components;
using Nexus.Application.Repositories;
using Nexus.Connectors.Core.Abstractions;
using Nexus.Core.Entities;
using Nexus.Core.Enums;

namespace Nexus.Web.Components.Pages;

public partial class Connections : ComponentBase
{
    private List<ConnectionItem> _connectionsList = [];

    [Inject]
    private IIntegrationRepository IntegrationRepository { get; set; } = default!;

    [Inject]
    private ISyncCheckpointRepository SyncCheckpointRepository { get; set; } = default!;

    [Inject]
    private ITaskRepository TaskRepository { get; set; } = default!;

    [Inject]
    private IConnectorRegistry ConnectorRegistry { get; set; } = default!;

    [Inject]
    private IConnectorSettingsStore ConnectorSettingsStore { get; set; } = default!;

    [Inject]
    private ITokenStore TokenStore { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IToastService ToastService { get; set; } = default!;

    private int ReadyCount => _connectionsList.Count(c => c.IsReady);

    private int TotalIntegrationCount => _connectionsList.Count;

    protected override async Task OnInitializedAsync()
    {
        await LoadConnectionsAsync();
    }

    private async Task LoadConnectionsAsync()
    {
        _connectionsList.Clear();

        foreach (var connector in ConnectorRegistry.GetAll())
        {
            foreach (var integration in await IntegrationRepository.GetByServiceTypeAsync(connector.ServiceType))
            {
                var surface = await connector.DescribeConnectionAsync(integration, ConnectorSettingsStore, TokenStore);
                _connectionsList.Add(new ConnectionItem
                {
                    Id = integration.Id,
                    Name = integration.DisplayName,
                    ServiceType = integration.ServiceType,
                    IsEnabled = integration.IsEnabled,
                    IsReady = integration.IsEnabled && surface.IsConfigurationComplete,
                    Settings = surface.Settings,
                });
            }
        }
    }

    private void AddNewConnection()
    {
        Navigation.NavigateTo("/settings");
    }

    private void EditConnection(Guid id)
    {
        Navigation.NavigateTo($"/settings?edit={id}");
    }

    private async Task ToggleConnection(Guid id)
    {
        var connection = _connectionsList.FirstOrDefault(c => c.Id == id);
        if (connection is null)
        {
            return;
        }

        var integration = await IntegrationRepository.GetByIdAsync(id);
        if (integration is null)
        {
            return;
        }

        integration.SetEnabled(!integration.IsEnabled);
        await IntegrationRepository.UpdateAsync(integration);
        await LoadConnectionsAsync();

        ToastService.ShowSuccess($"{(connection.IsEnabled ? "Disabled" : "Enabled")} {connection.Name}");
    }

    private async Task SyncNow(Guid id)
    {
        var connection = _connectionsList.FirstOrDefault(c => c.Id == id);
        if (connection is null)
        {
            return;
        }

        // Placeholder until sync endpoint is wired for per-integration manual trigger.
        ToastService.ShowInfo($"Syncing {connection.Name}...");
        await LoadConnectionsAsync();
    }

    private async Task DeleteConnection(Guid id)
    {
        var connection = _connectionsList.FirstOrDefault(c => c.Id == id);
        if (connection is null)
        {
            return;
        }

        await IntegrationRepository.DeleteAsync(id);
        await LoadConnectionsAsync();

        ToastService.ShowSuccess($"Deleted {connection.Name}");
    }

    private sealed class ConnectionItem
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public ServiceType ServiceType { get; set; }

        public bool IsEnabled { get; set; }

        public bool IsReady { get; set; }

        public object Settings { get; set; } = default!;
    }
}
