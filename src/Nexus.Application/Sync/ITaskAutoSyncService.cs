namespace Nexus.Application.Sync;

/// <summary>
/// Service interface for automatic task synchronization from external services.
/// </summary>
public interface ITaskAutoSyncService
{
    /// <summary>
    /// Synchronizes tasks from all configured services.
    /// </summary>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SyncAllAsync(CancellationToken ct = default);
}
