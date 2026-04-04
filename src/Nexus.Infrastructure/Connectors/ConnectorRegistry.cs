using Microsoft.Extensions.DependencyInjection;
using Nexus.Connectors.Core.Abstractions;
using Nexus.Connectors.Core.Models;
using Nexus.Core.ValueObjects;

namespace Nexus.Infrastructure.Connectors;

/// <summary>
/// Registry for managing and resolving service connector instances.
/// </summary>
public sealed class ConnectorRegistry(IServiceProvider serviceProvider) : IConnectorRegistry
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private IReadOnlyList<IServiceConnector>? _connectors;
    private IReadOnlyList<PluginManifest>? _manifests;

    /// <inheritdoc/>
    public IServiceConnector? Get(ServiceType type)
    {
        return _serviceProvider.GetKeyedService<IServiceConnector>(type);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IServiceConnector> GetAll()
    {
        _connectors ??= _serviceProvider.GetServices<IServiceConnector>().ToList().AsReadOnly();
        return _connectors;
    }

    /// <inheritdoc/>
    public PluginManifest? GetManifest(ServiceType type)
        => GetAllManifests().FirstOrDefault(manifest => manifest.ServiceType == type);

    /// <inheritdoc/>
    public IReadOnlyList<PluginManifest> GetAllManifests()
    {
        _manifests ??= GetAll()
            .Select(connector => connector.Manifest)
            .DistinctBy(manifest => manifest.ServiceType)
            .ToList()
            .AsReadOnly();
        return _manifests;
    }
}
