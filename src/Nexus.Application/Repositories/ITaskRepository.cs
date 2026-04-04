using Nexus.Core.Entities;
using Nexus.Core.ValueObjects;

namespace Nexus.Application.Repositories;

/// <summary>
/// Repository interface for managing unified tasks.
/// </summary>
public interface ITaskRepository
{
    /// <summary>
    /// Retrieves all unified tasks.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of all unified tasks.</returns>
    Task<IReadOnlyList<UnifiedTask>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves a unified task by its ID.
    /// </summary>
    /// <param name="id">The ID of the task.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The task, or null if not found.</returns>
    Task<UnifiedTask?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a unified task by its external reference.
    /// </summary>
    /// <param name="serviceType">The type of the external service.</param>
    /// <param name="externalId">The external ID of the task.</param>
    /// <param name="integrationId">Optional integration ID for filtering.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The task, or null if not found.</returns>
    Task<UnifiedTask?> GetByExternalRefAsync(ServiceType serviceType, string externalId, Guid? integrationId = null, CancellationToken ct = default);

    /// <summary>
    /// Adds a new unified task.
    /// </summary>
    /// <param name="task">The task to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(UnifiedTask task, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing unified task.
    /// </summary>
    /// <param name="task">The task to update.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync(UnifiedTask task, CancellationToken ct = default);

    /// <summary>
    /// Deletes a unified task by ID.
    /// </summary>
    /// <param name="id">The ID of the task to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
