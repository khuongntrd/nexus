using Nexus.Core.ValueObjects;

namespace Nexus.Web.Components.Connectors;

/// <summary>
/// Registry for managing UI components for different connectors.
/// </summary>
public interface IConnectorUiRegistry
{
    /// <summary>
    /// Gets the UI definition for a specific service type.
    /// </summary>
    /// <param name="serviceType">The service type to get the UI definition for.</param>
    /// <returns>The UI definition for the specified service type, or null if not found.</returns>
    ConnectorUiDefinition? Get(ServiceType serviceType);

    /// <summary>
    /// Gets all available UI definitions.
    /// </summary>
    /// <returns>A read-only list of all UI definitions.</returns>
    IReadOnlyList<ConnectorUiDefinition> GetAll();
}
