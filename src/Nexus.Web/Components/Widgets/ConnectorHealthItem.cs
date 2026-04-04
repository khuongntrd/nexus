namespace Nexus.Web.Components.Widgets;

/// <summary>
/// Represents the health status of a connector.
/// </summary>
public sealed record ConnectorHealthItem(

    /// <summary>The connector title.</summary>
    string Title,

    /// <summary>Additional details about the connector status.</summary>
    string Detail,

    /// <summary>The status label.</summary>
    string StatusLabel,

    /// <summary>The CSS class for the status.</summary>
    string StatusClass);
