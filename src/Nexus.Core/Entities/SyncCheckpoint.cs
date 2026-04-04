using Nexus.Core.ValueObjects;

namespace Nexus.Core.Entities;

/// <summary>
/// Entity representing a synchronization checkpoint for an external service, used to track incremental sync progress.
/// </summary>
public sealed class SyncCheckpoint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncCheckpoint"/> class with the specified parameters.
    /// </summary>
    /// <param name="serviceType">Service type for this checkpoint.</param>
    /// <param name="cursor">Optional cursor for incremental sync.</param>
    public SyncCheckpoint(ServiceType serviceType, string? cursor = null)
    {
        Id = Guid.NewGuid();
        ServiceType = serviceType;
        Cursor = cursor;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Unique identifier for the sync checkpoint.</summary>
    public Guid Id { get; private set; }

    /// <summary>Service type for this checkpoint.</summary>
    public ServiceType ServiceType { get; private set; }

    /// <summary>Cursor for incremental synchronization.</summary>
    public string? Cursor { get; private set; }

    /// <summary>Last update timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Updates the cursor for this checkpoint.
    /// </summary>
    /// <param name="cursor">New cursor value.</param>
    public void UpdateCursor(string? cursor)
    {
        Cursor = cursor;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

#pragma warning disable SA1201 // EF Core materialization ctor must follow the public API (SA1202).
    private SyncCheckpoint()
    {
    }
#pragma warning restore SA1201
}
