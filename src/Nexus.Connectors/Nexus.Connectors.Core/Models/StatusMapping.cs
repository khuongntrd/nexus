using UnifiedTaskStatus = Nexus.Core.Enums.TaskStatus;

namespace Nexus.Connectors.Core.Models;

/// <summary>
/// Represents the mapping between a unified task status and the corresponding external service status values.
/// </summary>
/// <param name="UnifiedStatus">The unified task status.</param>
/// <param name="ExternalStatuses">The external service status values that map to this unified status.</param>
public sealed record StatusMapping(
    UnifiedTaskStatus UnifiedStatus,
    IReadOnlyList<string> ExternalStatuses);
