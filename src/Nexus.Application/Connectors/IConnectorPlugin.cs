using Nexus.Core.Entities;
using Nexus.Core.ValueObjects;

namespace Nexus.Application.Connectors;

/// <summary>
/// Defines a connector plugin for integration services.
/// </summary>
public interface IConnectorPlugin
{
    /// <summary>
    /// Gets the type of service this connector supports.
    /// </summary>
    ServiceType ServiceType { get; }

    /// <summary>
    /// Saves the settings for the specified integration.
    /// </summary>
    /// <typeparam name="T">The type of the settings object.</typeparam>
    /// <param name="integration">The integration instance.</param>
    /// <param name="settings">The settings to save.</param>
    /// <param name="ct">A cancellation token.</param>
    Task SaveSettingsAsync<T>(Integration integration, T settings, CancellationToken ct = default)
        where T : class;

    /// <summary>
    /// Loads the settings for the specified integration.
    /// </summary>
    /// <typeparam name="T">The type of the settings object.</typeparam>
    /// <param name="integration">The integration instance.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The loaded settings, or null if not found.</returns>
    Task<T?> LoadSettingsAsync<T>(Integration integration, CancellationToken ct = default)
        where T : class;

    /// <summary>
    /// Validates the provided settings.
    /// </summary>
    /// <typeparam name="T">The type of the settings object.</typeparam>
    /// <param name="settings">The settings to validate.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>True if the settings are valid; otherwise, false.</returns>
    Task<bool> ValidateSettingsAsync<T>(T settings, CancellationToken ct = default)
        where T : class;

    /// <summary>
    /// Loads the settings for the specified integration using a runtime type.
    /// </summary>
    /// <param name="integration">The integration instance.</param>
    /// <param name="settingsType">The type of the settings object.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The loaded settings as an object, or null if not found.</returns>
    Task<object?> LoadSettingsAsync(Integration integration, Type settingsType, CancellationToken ct = default);

    /// <summary>
    /// Saves the settings for the specified integration using a runtime type.
    /// </summary>
    /// <param name="integration">The integration instance.</param>
    /// <param name="settings">The settings object to save.</param>
    /// <param name="ct">A cancellation token.</param>
    Task SaveSettingsAsync(Integration integration, object settings, CancellationToken ct = default);

    /// <summary>
    /// Validates the provided settings using a runtime type.
    /// </summary>
    /// <param name="settings">The settings object to validate.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>True if the settings are valid; otherwise, false.</returns>
    Task<bool> ValidateSettingsAsync(object settings, CancellationToken ct = default);
}
