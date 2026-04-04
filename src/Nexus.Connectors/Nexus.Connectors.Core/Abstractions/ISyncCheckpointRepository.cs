using Nexus.Core.Entities;
using Nexus.Core.ValueObjects;

namespace Nexus.Connectors.Core.Abstractions;

/// <summary>
/// Repository interface for managing sync checkpoint entities.
/// </summary>
public interface ISyncCheckpointRepository
{
    /// <summary>
    /// Gets the sync checkpoint for a given service type, or null if not found.
    /// </summary>
    /// <param name="serviceType">The service type to query for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sync checkpoint, or null if not found.</returns>
    Task<SyncCheckpoint?> GetByServiceTypeAsync(ServiceType serviceType, CancellationToken ct = default);

    /// <summary>
    /// Inserts or updates the given sync checkpoint.
    /// </summary>
    /// <param name="checkpoint">The checkpoint entity to upsert.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpsertAsync(SyncCheckpoint checkpoint, CancellationToken ct = default);
}
