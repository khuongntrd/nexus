using Nexus.Core.Enums;
using Nexus.Core.ValueObjects;
using TaskStatus = Nexus.Core.Enums.TaskStatus;

namespace Nexus.Core.Entities;

/// <summary>
/// Represents a unified task that can be synchronized across different services.
/// </summary>
public sealed class UnifiedTask
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnifiedTask"/> class with the specified parameters.
    /// </summary>
    /// <param name="title">Task title.</param>
    /// <param name="description">Optional task description.</param>
    /// <param name="status">Task status (default: Open).</param>
    /// <param name="priority">Optional task priority.</param>
    /// <param name="syncFromSource">Whether task was synced from source (default: true if externalRef is set).</param>
    /// <param name="dueAt">Optional due date.</param>
    /// <param name="externalRef">Optional external reference.</param>
    /// <param name="createdAt">Optional creation timestamp (default: current UTC time).</param>
    /// <param name="projectId">Optional project identifier.</param>
    public UnifiedTask(
        string title,
        string? description = null,
        TaskStatus status = TaskStatus.Open,
        TaskPriority? priority = null,
        bool? syncFromSource = null,
        DateTimeOffset? dueAt = null,
        ExternalRef? externalRef = null,
        DateTimeOffset? createdAt = null,
        Guid? projectId = null)
    {
        var now = DateTimeOffset.UtcNow;
        var normalizedCreatedAt = NormalizeUtc(createdAt) ?? now;
        Id = Guid.NewGuid();
        Title = title;
        Description = description;
        Status = status;
        Priority = priority;
        SyncFromSource = syncFromSource ?? externalRef is not null;
        DueAt = NormalizeUtc(dueAt);
        ExternalRef = externalRef;
        ProjectId = projectId;
        CreatedAt = normalizedCreatedAt;
        UpdatedAt = now;
    }

    /// <summary>Unique identifier for the unified task.</summary>
    public Guid Id { get; private set; }

    /// <summary>Title of the unified task.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Description of the unified task.</summary>
    public string? Description { get; private set; }

    /// <summary>Status of the unified task.</summary>
    public TaskStatus Status { get; private set; }

    /// <summary>Priority of the unified task.</summary>
    public TaskPriority? Priority { get; private set; }

    /// <summary>Whether the task was synced from a source.</summary>
    public bool SyncFromSource { get; private set; }

    /// <summary>Due date of the unified task.</summary>
    public DateTimeOffset? DueAt { get; private set; }

    /// <summary>External reference to the source task.</summary>
    public ExternalRef? ExternalRef { get; private set; }

    /// <summary>Associated project identifier.</summary>
    public Guid? ProjectId { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Last update timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Last synchronization timestamp.</summary>
    public DateTimeOffset? LastSyncAt { get; private set; }

    /// <summary>
    /// Updates the unified task with new values.
    /// </summary>
    /// <param name="title">Optional new title.</param>
    /// <param name="description">Optional new description.</param>
    /// <param name="status">Optional new status.</param>
    /// <param name="dueAt">Optional new due date.</param>
    /// <param name="createdAt">Optional new creation timestamp.</param>
    public void Update(
        string? title = null,
        string? description = null,
        TaskStatus? status = null,
        DateTimeOffset? dueAt = null,
        DateTimeOffset? createdAt = null)
    {
        if (title is not null)
        {
            Title = title;
        }

        if (description is not null)
        {
            Description = description;
        }

        if (status is not null)
        {
            Status = status.Value;
        }

        if (dueAt is not null)
        {
            DueAt = NormalizeUtc(dueAt);
        }

        if (createdAt is not null)
        {
            CreatedAt = NormalizeUtc(createdAt) ?? CreatedAt;
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets the external reference for the task.
    /// </summary>
    /// <param name="externalRef">External reference to set.</param>
    public void SetExternalRef(ExternalRef externalRef)
    {
        ExternalRef = externalRef;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets the priority for the task.
    /// </summary>
    /// <param name="priority">Priority to set.</param>
    public void SetPriority(TaskPriority? priority)
    {
        Priority = priority;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets whether the task was synced from source.
    /// </summary>
    /// <param name="syncFromSource">Sync from source flag.</param>
    public void SetSyncFromSource(bool syncFromSource)
    {
        SyncFromSource = syncFromSource;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the task as synced.
    /// </summary>
    /// <param name="syncedAt">Optional sync timestamp (default: current UTC time).</param>
    public void MarkSynced(DateTimeOffset? syncedAt = null)
    {
        LastSyncAt = NormalizeUtc(syncedAt) ?? DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Normalizes a date-time offset to UTC.
    /// </summary>
    /// <param name="value">Date-time offset to normalize.</param>
    /// <returns>UTC date-time offset or null if value is null.</returns>
    private static DateTimeOffset? NormalizeUtc(DateTimeOffset? value)
    {
        return value?.ToUniversalTime();
    }

#pragma warning disable SA1201 // EF Core materialization ctor must follow the public API (SA1202).
    private UnifiedTask()
    {
    }
#pragma warning restore SA1201
}
