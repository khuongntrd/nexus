using Nexus.Connectors.Core.Models;
using Nexus.Core.Entities;
using Nexus.Core.Enums;
using Nexus.Core.ValueObjects;
using TaskStatusEnum = Nexus.Core.Enums.TaskStatus;

namespace Nexus.Connectors.Core.Abstractions;

/// <summary>
/// Interface for service connectors that integrate with external services.
/// </summary>
public interface IServiceConnector
{
    /// <summary>
    /// The service type this connector supports.
    /// </summary>
    ServiceType ServiceType { get; }

    /// <summary>
    /// The display name for the connector.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// The authentication modes supported by this connector.
    /// </summary>
    AuthMode[] SupportedAuthModes { get; }

    /// <summary>
    /// The OAuth scopes required by this connector.
    /// </summary>
    string[] RequiredScopes { get; }

    /// <summary>
    /// The plugin manifest describing this connector.
    /// </summary>
    PluginManifest Manifest { get; }

    /// <summary>
    /// When false, task edits stay local-only (no remote item update).
    /// </summary>
    bool SupportsRemoteTaskMutation => true;

    /// <summary>
    /// When true, OAuth redirect URI must come from integration settings (not the app default).
    /// </summary>
    bool RequiresCustomOAuthRedirectUri => false;

    /// <summary>
    /// Skips auto-sync for this integration when preconditions are not met (e.g. missing config or token).
    /// </summary>
    /// <param name="integration">Integration row.</param>
    /// <returns>True if auto-sync should not run.</returns>
    bool ShouldSkipAutoSyncFor(Integration integration) => false;

    /// <summary>
    /// Describes connection readiness and settings for the settings UI.
    /// </summary>
    /// <param name="integration">Integration row.</param>
    /// <param name="settingsStore">Settings persistence.</param>
    /// <param name="tokenStore">OAuth token store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Snapshot for the connection row.</returns>
    Task<ConnectionSurface> DescribeConnectionAsync(
        Integration integration,
        IConnectorSettingsStore settingsStore,
        ITokenStore tokenStore,
        CancellationToken ct = default);

    /// <summary>
    /// Fetches a single remote item, or null if not found / not supported.
    /// </summary>
    /// <param name="integration">Integration context.</param>
    /// <param name="externalId">External identifier.</param>
    /// <param name="since">Optional lower bound for list-based implementations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Matching item, or null.</returns>
    Task<ServiceItem?> FetchItemByExternalIdAsync(
        Integration integration,
        string externalId,
        DateTimeOffset? since,
        CancellationToken ct = default) => Task.FromResult<ServiceItem?>(null);

    /// <summary>
    /// Optional accent badge for list/detail headers (e.g. repo#issue).
    /// </summary>
    /// <param name="externalRef">External reference, if any.</param>
    /// <returns>Badge text, or null.</returns>
    string? GetListAccentBadge(ExternalRef? externalRef) => null;

    /// <summary>
    /// Builds a source URL when <see cref="ExternalRef.Url"/> is empty.
    /// </summary>
    /// <param name="externalId">External identifier.</param>
    /// <returns>URL, or null.</returns>
    string? BuildFallbackSourceUrl(string externalId) => null;

    /// <summary>
    /// When non-null, replaces the default OAuth redirect URI (e.g. per-integration app registration).
    /// </summary>
    /// <param name="integrationId">Integration id.</param>
    /// <param name="defaultRedirectUri">Application default callback URL.</param>
    /// <param name="settingsStore">Settings store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Override redirect URI, or null to use <paramref name="defaultRedirectUri"/>.</returns>
    Task<string?> ResolveOAuthRedirectUriAsync(
        Guid integrationId,
        string defaultRedirectUri,
        IConnectorSettingsStore settingsStore,
        CancellationToken ct = default) => Task.FromResult<string?>(null);

    /// <summary>
    /// Whether this connector proxies authenticated GitHub-style attachment asset URLs.
    /// </summary>
    /// <param name="assetUri">Requested asset URI.</param>
    /// <returns>True when the host/path is supported.</returns>
    bool IsAttachmentProxyUrl(Uri assetUri) => false;

    /// <summary>
    /// Gets a bearer token suitable for <see cref="IsAttachmentProxyUrl"/> requests.
    /// </summary>
    /// <param name="integration">Integration to read credentials from.</param>
    /// <param name="settingsStore">Settings store.</param>
    /// <param name="tokenStore">Token store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Access token, or null.</returns>
    Task<string?> GetAttachmentProxyAccessTokenAsync(
        Integration integration,
        IConnectorSettingsStore settingsStore,
        ITokenStore tokenStore,
        CancellationToken ct = default) => GetAccessTokenAsync(integration, settingsStore, tokenStore, ct);

    /// <summary>
    /// Resolves the connector credential as an access token (PAT or OAuth access token).
    /// </summary>
    /// <param name="integration">Integration row.</param>
    /// <param name="settingsStore">Settings persistence.</param>
    /// <param name="tokenStore">OAuth token store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Access token string, or null when unavailable.</returns>
    Task<string?> GetAccessTokenAsync(
        Integration integration,
        IConnectorSettingsStore settingsStore,
        ITokenStore tokenStore,
        CancellationToken ct = default);

    /// <summary>
    /// Fetches all remote items for the integration, optionally since a given date.
    /// </summary>
    /// <param name="integration">Integration context.</param>
    /// <param name="since">Optional lower bound for incremental sync.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of remote items.</returns>
    Task<IReadOnlyList<ServiceItem>> FetchItemsAsync(Integration integration, DateTimeOffset? since, CancellationToken ct = default);

    /// <summary>
    /// Creates a new remote item in the external service.
    /// </summary>
    /// <param name="integration">Integration context.</param>
    /// <param name="title">Item title.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="dueAt">Optional due date.</param>
    /// <param name="projectKey">Optional project/repository key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created remote item.</returns>
    Task<ServiceItem> CreateItemAsync(
        Integration integration,
        string title,
        string? description = null,
        DateTimeOffset? dueAt = null,
        string? projectKey = null,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an existing remote item in the external service.
    /// </summary>
    /// <param name="integration">Integration context.</param>
    /// <param name="externalId">External identifier.</param>
    /// <param name="status">Optional new status.</param>
    /// <param name="title">Optional new title.</param>
    /// <param name="description">Optional new description.</param>
    /// <param name="dueAt">Optional new due date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated remote item.</returns>
    Task<ServiceItem> UpdateItemAsync(
        Integration integration,
        string externalId,
        TaskStatusEnum? status = null,
        string? title = null,
        string? description = null,
        DateTimeOffset? dueAt = null,
        CancellationToken ct = default);

    /// <summary>
    /// Adds a comment to a remote item in the external service.
    /// </summary>
    /// <param name="integration">Integration context.</param>
    /// <param name="externalId">External identifier.</param>
    /// <param name="body">Comment body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddCommentAsync(Integration integration, string externalId, string body, CancellationToken ct = default);

    /// <summary>
    /// Closes a remote item in the external service.
    /// </summary>
    /// <param name="integration">Integration context.</param>
    /// <param name="externalId">External identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CloseItemAsync(Integration integration, string externalId, CancellationToken ct = default);

    /// <summary>
    /// Maps a remote service item to a unified task.
    /// </summary>
    /// <param name="item">Remote service item.</param>
    /// <param name="projectId">Optional project id.</param>
    /// <param name="integrationId">Optional integration id.</param>
    /// <returns>Unified task object.</returns>
    UnifiedTask MapToUnifiedTask(ServiceItem item, Guid? projectId = null, Guid? integrationId = null);

    /// <summary>
    /// Maps a unified task to a remote service item.
    /// </summary>
    /// <param name="task">Unified task object.</param>
    /// <returns>Remote service item.</returns>
    ServiceItem MapFromUnifiedTask(UnifiedTask task);
}
