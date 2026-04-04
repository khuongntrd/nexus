using Nexus.Core.Entities;

namespace Nexus.Connectors.Core.Abstractions;

/// <summary>
/// Abstraction for connector-specific UI configuration logic.
/// Each connector provides an implementation to handle its own settings, validation, and parameters.
/// </summary>
public interface IConnectorUiConfig
{
    /// <summary>
    /// Gets the type of settings object for this connector.
    /// </summary>
    /// <returns>The settings type for this connector.</returns>
    Type GetSettingsType();

    /// <summary>
    /// Loads settings from an integration into a settings dictionary.
    /// </summary>
    /// <param name="integration">The integration to load settings from.</param>
    /// <returns>A dictionary containing the loaded settings.</returns>
    Task<Dictionary<string, object>> LoadSettingsAsync(Integration integration);

    /// <summary>
    /// Normalizes settings before saving (e.g., splitting text into arrays).
    /// </summary>
    /// <param name="settings">The settings dictionary to normalize.</param>
    void NormalizeSettings(Dictionary<string, object> settings);

    /// <summary>
    /// Gets the display name for the connection from settings or falls back to connector name.
    /// </summary>
    /// <param name="settings">The settings dictionary.</param>
    /// <param name="fallback">The fallback display name.</param>
    /// <param name="connectorDisplayName">The connector display name.</param>
    /// <returns>The display name for the connection.</returns>
    string GetDisplayName(Dictionary<string, object> settings, string? fallback, string connectorDisplayName);

    /// <summary>
    /// Validates that the settings are sufficiently configured.
    /// </summary>
    /// <param name="settings">The settings dictionary.</param>
    /// <param name="tokenJson">The token JSON, if any.</param>
    /// <returns>True if settings are configured; otherwise, false.</returns>
    bool IsConfigured(Dictionary<string, object> settings, string? tokenJson);

    /// <summary>
    /// Builds the parameters dictionary to pass to the config component.
    /// </summary>
    /// <param name="settings">The settings dictionary.</param>
    /// <param name="onSettingsChanged">Optional callback when settings change.</param>
    /// <returns>A dictionary of component parameters.</returns>
    IDictionary<string, object> BuildComponentParameters(
        Dictionary<string, object> settings,
        object? onSettingsChanged = null);

    /// <summary>
    /// Gets a key for forcing component remount when settings change (e.g., for auth mode switches).
    /// </summary>
    /// <param name="settings">The settings dictionary.</param>
    /// <returns>A key used to remount the component when settings change.</returns>
    object GetComponentKey(Dictionary<string, object> settings);
}
