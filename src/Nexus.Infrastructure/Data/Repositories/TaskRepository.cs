using Microsoft.EntityFrameworkCore;
using Nexus.Application.Repositories;
using Nexus.Core.Entities;
using Nexus.Core.ValueObjects;

namespace Nexus.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for managing unified tasks.
/// </summary>
public sealed class TaskRepository(NexusDbContext db) : ITaskRepository
{
    private readonly NexusDbContext _db = db;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UnifiedTask>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Tasks.AsNoTracking().ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<UnifiedTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    /// <inheritdoc/>
    public async Task<UnifiedTask?> GetByExternalRefAsync(ServiceType serviceType, string externalId, Guid? integrationId = null, CancellationToken ct = default)
    {
        return await _db.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.ExternalRef != null
                     && t.ExternalRef.ServiceType == serviceType
                     && t.ExternalRef.ExternalId == externalId
                     && (integrationId == null || t.ExternalRef.IntegrationId == integrationId),
                ct);
    }

    /// <inheritdoc/>
    public async Task AddAsync(UnifiedTask task, CancellationToken ct = default)
    {
        await _db.Tasks.AddAsync(task, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(UnifiedTask task, CancellationToken ct = default)
    {
        var tracked = _db.Tasks.Local.FirstOrDefault(t => t.Id == task.Id);

        if (tracked is null)
        {
            _db.Tasks.Attach(task);
            _db.Entry(task).State = EntityState.Modified;
        }
        else
        {
            _db.Entry(tracked).CurrentValues.SetValues(task);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var task = await _db.Tasks.FindAsync([id], ct);
        if (task is not null)
        {
            _db.Tasks.Remove(task);
            await _db.SaveChangesAsync(ct);
        }
    }
}
