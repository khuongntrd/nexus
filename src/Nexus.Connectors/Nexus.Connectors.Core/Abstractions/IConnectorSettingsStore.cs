namespace Nexus.Connectors.Core.Abstractions;

/// <summary>
/// Interface for storing and retrieving connector-specific settings.
/// </summary>
public interface IConnectorSettingsStore
{
    /// <summary>
    /// Retrieves connector settings for a specific integration.
    /// </summary>
    /// <typeparam name="T">The settings type to retrieve.</typeparam>
    /// <param name="integrationId">The integration ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The settings, or null if not found.</returns>
    Task<T?> GetAsync<T>(Guid integrationId, CancellationToken ct = default)
        where T : class;

    /// <summary>
    /// Saves connector settings for a specific integration.
    /// </summary>
    /// <typeparam name="T">The settings type to save.</typeparam>
    /// <param name="integrationId">The integration ID.</param>
    /// <param name="settings">The settings to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveAsync<T>(Guid integrationId, T settings, CancellationToken ct = default)
        where T : class;

    /// <summary>
    /// Deletes connector settings for a specific integration.
    /// </summary>
    /// <param name="integrationId">The integration ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid integrationId, CancellationToken ct = default);
}
