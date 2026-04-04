using Nexus.Core.ValueObjects;

namespace Nexus.Application.Connectors;

/// <summary>
/// Registry that provides connector plugin instances for specific service types.
/// </summary>
public interface IConnectorPluginRegistry
{
    /// <summary>
    /// Gets the connector plugin for a specific service type.
    /// </summary>
    /// <param name="serviceType">The service type to get the plugin for.</param>
    /// <returns>The connector plugin for the specified service type, or null if not found.</returns>
    IConnectorPlugin? Get(ServiceType serviceType);

    /// <summary>
    /// Gets all available connector plugins.
    /// </summary>
    /// <returns>A read-only list of all available connector plugins.</returns>
    IReadOnlyList<IConnectorPlugin> GetAll();
}
