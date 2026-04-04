using Nexus.Connectors.Core.Models;
using Nexus.Core.ValueObjects;

namespace Nexus.Application.Connectors;

/// <summary>
/// Provides access to connector plugin manifests for different service types.
/// </summary>
public interface IConnectorPluginCatalog
{
    /// <summary>
    /// Retrieves the plugin manifest for a specific service type.
    /// </summary>
    /// <param name="serviceType">The service type to get the manifest for.</param>
    /// <returns>The plugin manifest for the specified service type, or null if not found.</returns>
    PluginManifest? Get(ServiceType serviceType);

    /// <summary>
    /// Gets all available plugin manifests.
    /// </summary>
    /// <returns>A read-only list of all available plugin manifests.</returns>
    IReadOnlyList<PluginManifest> GetAll();
}
