namespace Nexus.Core.Enums;

/// <summary>
/// Represents the status of a task in the unified task list.
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// The task is open and not yet started.
    /// </summary>
    Open,

    /// <summary>
    /// The task is currently in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// The task is completed.
    /// </summary>
    Done,

    /// <summary>
    /// The task is blocked and cannot be completed.
    /// </summary>
    Blocked,

    /// <summary>
    /// The task was cancelled.
    /// </summary>
    Cancelled,
}
