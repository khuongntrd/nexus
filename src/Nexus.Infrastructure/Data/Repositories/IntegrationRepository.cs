using Microsoft.EntityFrameworkCore;
using Nexus.Application.Repositories;
using Nexus.Core.Entities;
using Nexus.Core.ValueObjects;

namespace Nexus.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for managing integrations in the Nexus application.
/// </summary>
/// <param name="db">The database context instance to be used by the repository.</param>
public sealed class IntegrationRepository(NexusDbContext db) : IIntegrationRepository
{
    private readonly NexusDbContext _db = db;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Integration>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Integrations.AsNoTracking().ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Integration>> GetByServiceTypeAsync(ServiceType serviceType, CancellationToken ct = default)
    {
        return await _db.Integrations
            .AsNoTracking()
            .Where(i => i.ServiceType == serviceType)
            .OrderBy(i => i.DisplayName)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<Integration?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Integrations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Integration integration, CancellationToken ct = default)
    {
        await _db.Integrations.AddAsync(integration, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Integration integration, CancellationToken ct = default)
    {
        var tracked = _db.Integrations.Local.FirstOrDefault(i => i.Id == integration.Id);

        if (tracked is null)
        {
            _db.Integrations.Attach(integration);
            _db.Entry(integration).State = EntityState.Modified;
        }
        else
        {
            _db.Entry(tracked).CurrentValues.SetValues(integration);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var integration = await _db.Integrations.FindAsync([id], ct);
        if (integration != null)
        {
            _db.Integrations.Remove(integration);
            await _db.SaveChangesAsync(ct);
        }
    }
}
