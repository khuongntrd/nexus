using Nexus.Connectors.Core.Abstractions;
using Nexus.Core.ValueObjects;

namespace Nexus.Web.Components.Connectors;

/// <summary>
/// Defines the UI component and display name for a connector.
/// </summary>
public sealed record ConnectorUiDefinition(

    /// <summary>The service type for this connector.</summary>
    ServiceType ServiceType,

    /// <summary>The display name for the connector.</summary>
    string DisplayName,

    /// <summary>The type of the configuration component.</summary>
    Type ConfigComponentType,

    /// <summary>The UI config handler for this connector.</summary>
    IConnectorUiConfig UiConfig);
