using System.ComponentModel.DataAnnotations;

namespace Nexus.Application.Options;

/// <summary>
/// Configuration options for automatic task synchronization.
/// </summary>
public sealed record SyncOptions
{
    public const string SectionName = "Sync";

    /// <summary>
    /// Whether automatic synchronization is enabled.
    /// </summary>
    public bool AutoSyncEnabled { get; set; } = true;

    /// <summary>
    /// Whether to run sync on application startup.
    /// </summary>
    public bool RunOnStartup { get; set; }

    /// <summary>
    /// The interval in seconds between automatic sync runs.
    /// </summary>
    [Range(30, 86400)]
    public int AutoSyncIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// The default number of days to look back for tasks during sync.
    /// </summary>
    [Range(1, 365)]
    public int DefaultLookbackDays { get; set; } = 30;
}
