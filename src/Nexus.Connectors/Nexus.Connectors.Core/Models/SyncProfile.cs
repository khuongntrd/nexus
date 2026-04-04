namespace Nexus.Connectors.Core.Models;

/// <summary>
/// Represents the synchronization profile of a service connector.
/// </summary>
/// <param name="DefaultLookbackDays">The default number of days to look back for synchronization.</param>
/// <param name="SupportsIncrementalSync">Whether the connector supports incremental sync.</param>
/// <param name="SupportsManualSync">Whether the connector supports manual synchronization.</param>
/// <param name="SupportsAutoSync">Whether the connector supports automatic synchronization.</param>
public sealed record SyncProfile(
    int DefaultLookbackDays,
    bool SupportsIncrementalSync,
    bool SupportsManualSync,
    bool SupportsAutoSync);
