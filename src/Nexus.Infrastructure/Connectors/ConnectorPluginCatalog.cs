using Nexus.Application.Connectors;
using Nexus.Connectors.Core.Abstractions;
using Nexus.Connectors.Core.Models;
using Nexus.Core.ValueObjects;

namespace Nexus.Infrastructure.Connectors;

/// <summary>
/// Implementation of IConnectorPluginCatalog that provides a catalog of connector plugins with their configurations.
/// </summary>
public sealed class ConnectorPluginCatalog(IConnectorRegistry connectorRegistry) : IConnectorPluginCatalog
{
    private readonly IConnectorRegistry _connectorRegistry = connectorRegistry;

    /// <inheritdoc/>
    public PluginManifest? Get(ServiceType serviceType)
        => _connectorRegistry.GetManifest(serviceType);

    /// <inheritdoc/>
    public IReadOnlyList<PluginManifest> GetAll()
        => _connectorRegistry.GetAllManifests();
}
