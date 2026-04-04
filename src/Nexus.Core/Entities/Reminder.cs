namespace Nexus.Core.Entities;

/// <summary>
/// Represents a scheduled reminder for a task that will fire at a specific time.
/// </summary>
public sealed class Reminder
{
    /// <summary>Unique identifier for the reminder.</summary>
    public Guid Id { get; set; }

    /// <summary>Associated task identifier.</summary>
    public Guid TaskId { get; set; }

    /// <summary>Reminder message.</summary>
    public string? Message { get; set; }

    /// <summary>When the reminder is due.</summary>
    public DateTimeOffset DueAt { get; set; }

    /// <summary>Whether the reminder has been fired.</summary>
    public bool IsFired { get; set; }

    /// <summary>
    /// Private constructor to prevent direct instantiation.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the reminder should fire.</summary>
    public DateTimeOffset? FireAt { get; set; }

    /// <summary>
    /// Updates the reminder with new values.
    /// </summary>
    /// <param name="message">Optional new message.</param>
    /// <param name="dueAt">Optional new due date.</param>
    public void Update(string? message = null, DateTimeOffset? dueAt = null)
    {
        if (message is not null)
        {
            Message = message;
        }

        if (dueAt is not null)
        {
            DueAt = dueAt.Value;
        }
    }
}
