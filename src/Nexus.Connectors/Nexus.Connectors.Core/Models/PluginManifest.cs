using Nexus.Core.Enums;
using Nexus.Core.ValueObjects;

namespace Nexus.Connectors.Core.Models;

/// <summary>
/// Manifest describing the capabilities and configuration of a service connector plugin.
/// </summary>
/// <param name="ServiceType">The service type this connector supports.</param>
/// <param name="DisplayName">The display name for the connector.</param>
/// <param name="SupportedAuthModes">The authentication modes supported by this connector.</param>
/// <param name="RequiredScopes">The OAuth scopes required by this connector.</param>
/// <param name="SyncProfile">The sync profile capabilities for this connector.</param>
/// <param name="ConfigSchema">The configuration schema fields for this connector.</param>
/// <param name="StatusMappings">The status mappings for this connector.</param>
/// <param name="PullRequestExternalIdMarker">Optional substring that identifies pull-request tasks in <see cref="ServiceItem.ExternalId"/>.</param>
/// <param name="MapsDoneStatusToClosedLabel">When true, unified <c>Done</c> status is shown as Closed in the UI.</param>
/// <param name="WhenSingleItemPullReturnsNullMarkLocalTaskDone">When true, a null single-item pull means the local task should be marked done (e.g. unassigned filter).</param>
public sealed record PluginManifest(
    ServiceType ServiceType,
    string DisplayName,
    IReadOnlyList<AuthMode> SupportedAuthModes,
    IReadOnlyList<string> RequiredScopes,
    SyncProfile SyncProfile,
    IReadOnlyList<ConfigFieldSchema> ConfigSchema,
    IReadOnlyList<StatusMapping> StatusMappings,
    string? PullRequestExternalIdMarker = null,
    bool MapsDoneStatusToClosedLabel = false,
    bool WhenSingleItemPullReturnsNullMarkLocalTaskDone = false);
