using Microsoft.EntityFrameworkCore;
using Nexus.Connectors.Core.Abstractions;
using Nexus.Core.Entities;
using Nexus.Core.ValueObjects;

namespace Nexus.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for managing sync checkpoints across different service types.
/// </summary>
public sealed class SyncCheckpointRepository(NexusDbContext db) : ISyncCheckpointRepository
{
    private readonly NexusDbContext _db = db;

    /// <inheritdoc/>
    public async Task<SyncCheckpoint?> GetByServiceTypeAsync(ServiceType serviceType, CancellationToken ct = default)
    {
        return await _db.SyncCheckpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ServiceType == serviceType, ct);
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(SyncCheckpoint checkpoint, CancellationToken ct = default)
    {
        var existing = await _db.SyncCheckpoints
            .FirstOrDefaultAsync(s => s.ServiceType == checkpoint.ServiceType, ct);

        if (existing is null)
        {
            await _db.SyncCheckpoints.AddAsync(checkpoint, ct);
        }
        else
        {
            existing.UpdateCursor(checkpoint.Cursor);
            _db.SyncCheckpoints.Update(existing);
        }

        await _db.SaveChangesAsync(ct);
    }
}
