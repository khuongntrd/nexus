using Nexus.Connectors.Core.Models;
using Nexus.Core.ValueObjects;

namespace Nexus.Connectors.Core.Abstractions;

/// <summary>
/// Registry for managing service connector instances.
/// </summary>
public interface IConnectorRegistry
{
    /// <summary>
    /// Gets a connector for a specific service type.
    /// </summary>
    /// <param name="type">The service type.</param>
    /// <returns>The connector, or null if not found.</returns>
    IServiceConnector? Get(ServiceType type);

    /// <summary>
    /// Gets all available connectors.
    /// </summary>
    /// <returns>A read-only list of all connectors.</returns>
    IReadOnlyList<IServiceConnector> GetAll();

    /// <summary>
    /// Gets a connector manifest for a specific service type.
    /// </summary>
    /// <param name="type">The service type.</param>
    /// <returns>The manifest, or null if not found.</returns>
    PluginManifest? GetManifest(ServiceType type);

    /// <summary>
    /// Gets all connector manifests.
    /// </summary>
    /// <returns>A read-only list of manifests.</returns>
    IReadOnlyList<PluginManifest> GetAllManifests();
}
