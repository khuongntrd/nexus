using Nexus.Core.Entities;
using Nexus.Core.ValueObjects;

namespace Nexus.Application.Repositories;

/// <summary>
/// Repository interface for managing service integrations.
/// </summary>
public interface IIntegrationRepository
{
    /// <summary>
    /// Retrieves all service integrations.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of all integrations.</returns>
    Task<IReadOnlyList<Integration>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves integrations for a specific service type.
    /// </summary>
    /// <param name="serviceType">The service type to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of integrations for the specified service type.</returns>
    Task<IReadOnlyList<Integration>> GetByServiceTypeAsync(ServiceType serviceType, CancellationToken ct = default);

    /// <summary>
    /// Retrieves an integration by its ID.
    /// </summary>
    /// <param name="id">The ID of the integration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The integration, or null if not found.</returns>
    Task<Integration?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Adds a new integration.
    /// </summary>
    /// <param name="integration">The integration to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(Integration integration, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing integration.
    /// </summary>
    /// <param name="integration">The integration to update.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(Integration integration, CancellationToken ct = default);

    /// <summary>
    /// Deletes an integration by ID.
    /// </summary>
    /// <param name="id">The ID of the integration to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
