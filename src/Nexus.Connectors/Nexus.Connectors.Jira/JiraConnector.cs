using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Nexus.Connectors.Core.Abstractions;
using Nexus.Connectors.Core.Models;
using Nexus.Connectors.Core.OAuth;
using Nexus.Connectors.Jira.Options;
using Nexus.Core.Entities;
using Nexus.Core.Enums;
using Nexus.Core.ValueObjects;
using TaskStatus = Nexus.Core.Enums.TaskStatus;

#pragma warning disable SA1204 // Static helper grouping is intentional for readability.

namespace Nexus.Connectors.Jira;

/// <summary>
/// Connector implementation for Jira service integration.
/// </summary>
/// <param name="settingsStore">Encrypted settings for integrations.</param>
/// <param name="checkpoints">Sync checkpoint persistence.</param>
/// <param name="httpClientFactory">HTTP client factory for Jira API calls.</param>
public sealed class JiraConnector(
    IConnectorSettingsStore settingsStore,
    ISyncCheckpointRepository checkpoints,
    IHttpClientFactory httpClientFactory) : IServiceConnector
{
    private static readonly string[] SearchFields =
    [
        "summary",
        "description",
        "priority",
        "assignee",
        "status",
        "duedate",
        "updated",
        "created",
        "project"
    ];

    private readonly IConnectorSettingsStore _settingsStore = settingsStore;
    private readonly ISyncCheckpointRepository _checkpoints = checkpoints;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    /// <summary>
    /// Canonical service key for this connector (matches plugin manifest).
    /// </summary>
    public static ServiceType RegisteredServiceType { get; } = new("jira");

    /// <summary>
    /// Gets the service type for Jira integration.
    /// </summary>
    public ServiceType ServiceType => RegisteredServiceType;

    /// <summary>
    /// Gets the display name for Jira service.
    /// </summary>
    public string DisplayName => "Jira";

    /// <summary>
    /// Gets the authentication modes supported by Jira.
    /// </summary>
    public AuthMode[] SupportedAuthModes => [AuthMode.PersonalAccessToken];

    /// <summary>
    /// Gets the required OAuth scopes for Jira API access.
    /// </summary>
    public string[] RequiredScopes => [];

    /// <inheritdoc/>
    public PluginManifest Manifest { get; } = new(
        RegisteredServiceType,
        "Jira",
        [AuthMode.PersonalAccessToken],
        [],
        new SyncProfile(30, SupportsIncrementalSync: true, SupportsManualSync: true, SupportsAutoSync: true),
        [
            new ConfigFieldSchema("DisplayName", "Display Name", "text", IsRequired: true),
            new ConfigFieldSchema("BaseUrl", "Base URL", "url", IsRequired: true),
            new ConfigFieldSchema("WebUrl", "Web URL", "url", IsRequired: false),
            new ConfigFieldSchema("Email", "Email", "text", IsRequired: true),
            new ConfigFieldSchema("ApiToken", "API Token", "password", IsRequired: true),
            new ConfigFieldSchema("ProjectKeys", "Project Keys", "multiline", IsRequired: false),
            new ConfigFieldSchema("Jql", "JQL", "multiline", IsRequired: false),
            new ConfigFieldSchema("DefaultIssueType", "Default Issue Type", "text", IsRequired: true)
        ],
        [
            new StatusMapping(TaskStatus.Open, ["to do", "open", "backlog"]),
            new StatusMapping(TaskStatus.InProgress, ["in progress"]),
            new StatusMapping(TaskStatus.Done, ["done", "resolved", "closed"])
        ],
        PullRequestExternalIdMarker: null,
        MapsDoneStatusToClosedLabel: true,
        WhenSingleItemPullReturnsNullMarkLocalTaskDone: true);

    /// <inheritdoc/>
    public bool ShouldSkipAutoSyncFor(Integration integration)
        => string.IsNullOrWhiteSpace(integration.ConfigJson);

    /// <inheritdoc/>
    public async Task<ConnectionSurface> DescribeConnectionAsync(
        Integration integration,
        IConnectorSettingsStore settingsStore,
        ITokenStore tokenStore,
        CancellationToken ct = default)
    {
        _ = tokenStore;
        var settings = await settingsStore.GetAsync<JiraOptions>(integration.Id, ct)
            ?? new JiraOptions { DefaultIssueType = "Task" };
        settings.DisplayName = integration.DisplayName;
        settings.ProjectKeysText = string.Join(
            Environment.NewLine,
            settings.ProjectKeys.Where(k => !string.IsNullOrWhiteSpace(k)));
        return new ConnectionSurface(true, IsJiraReady(settings), settings);
    }

    /// <inheritdoc/>
    public Task<ServiceItem?> FetchItemByExternalIdAsync(
        Integration integration,
        string externalId,
        DateTimeOffset? since,
        CancellationToken ct = default)
        => FetchTrackedItemByExternalIdAsync(integration.Id, externalId, ct);

    /// <inheritdoc/>
    public string? GetListAccentBadge(ExternalRef? externalRef)
    {
        if (externalRef is null || externalRef.ServiceType != ServiceType)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(externalRef.ExternalId) ? null : externalRef.ExternalId;
    }

    /// <inheritdoc/>
    public async Task<string?> GetAccessTokenAsync(
        Integration integration,
        IConnectorSettingsStore settingsStore,
        ITokenStore tokenStore,
        CancellationToken ct = default)
    {
        _ = tokenStore;
        var settings = await settingsStore.GetAsync<JiraOptions>(integration.Id, ct);
        return settings is null || string.IsNullOrWhiteSpace(settings.ApiToken) ? null : settings.ApiToken;
    }

    /// <summary>
    /// Builds the authorization URL for OAuth PKCE flow.
    /// </summary>
    /// <param name="pkce">OAuth PKCE parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Authorization URL.</returns>
    /// <exception cref="NotSupportedException">Thrown when OAuth is not implemented.</exception>
    public Task<Uri> BuildAuthorizationUrlAsync(OAuthPkceParams pkce, CancellationToken ct = default)
        => throw new NotSupportedException("Jira uses API token based basic authentication in this phase.");

    /// <summary>
    /// Exchanges authorization code for access token.
    /// </summary>
    /// <param name="code">Authorization code from OAuth flow.</param>
    /// <param name="pkce">OAuth PKCE parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Token set with access token.</returns>
    /// <exception cref="NotSupportedException">Thrown when OAuth is not implemented.</exception>
    public Task<TokenSet> ExchangeCodeAsync(string code, OAuthPkceParams pkce, CancellationToken ct = default)
        => throw new NotSupportedException("Jira OAuth is not implemented in this phase.");

    /// <summary>
    /// Refreshes the access token using the refresh token.
    /// </summary>
    /// <param name="current">Current token set.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated token set.</returns>
    /// <exception cref="NotSupportedException">Thrown when token refresh is not supported.</exception>
    public Task<TokenSet> RefreshTokenAsync(TokenSet current, CancellationToken ct = default)
        => throw new NotSupportedException("Jira API tokens do not refresh.");

    /// <summary>
    /// Fetches items from configured Jira projects.
    /// </summary>
    /// <param name="integration">Integration configuration.</param>
    /// <param name="since">Sync threshold timestamp.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of service items.</returns>
    public async Task<IReadOnlyList<ServiceItem>> FetchItemsAsync(Integration integration, DateTimeOffset? since, CancellationToken ct = default)
    {
        var settings = await _settingsStore.GetAsync<JiraOptions>(integration.Id, ct);
        if (!IsConfigured(settings))
        {
            return [];
        }

        var client = CreateClient(settings!);
        var result = new List<ServiceItem>();

        var includeHistoricalAssignees = integration.LastSyncAt is not null;
        if (!await TryFetchEnhancedSearchAsync(client, settings!, BuildJql(settings!, since, includeHistoricalAssignees), result, ct))
        {
            await FetchLegacySearchAsync(client, settings!, BuildJql(settings!, since, includeHistoricalAssignees), result, ct);
        }

        await _checkpoints.UpsertAsync(
            new SyncCheckpoint(ServiceType, DateTimeOffset.UtcNow.ToString("O")),
            ct);

        return result;
    }

    /// <summary>
    /// Creates a new issue in a Jira project.
    /// </summary>
    /// <param name="integration">Integration configuration.</param>
    /// <param name="title">Item title.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="dueAt">Optional due date.</param>
    /// <param name="projectKey">Optional project key; falls back to connector defaults.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Created service item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Jira is not configured or project key is missing.</exception>
    public async Task<ServiceItem> CreateItemAsync(
        Integration integration,
        string title,
        string? description = null,
        DateTimeOffset? dueAt = null,
        string? projectKey = null,
        CancellationToken ct = default)
    {
        var settings = await _settingsStore.GetAsync<JiraOptions>(integration.Id, ct);
        if (!IsConfigured(settings))
        {
            throw new InvalidOperationException("Jira is not configured.");
        }

        projectKey = string.IsNullOrWhiteSpace(projectKey)
            ? settings!.ProjectKeys.FirstOrDefault()
            : projectKey;

        if (string.IsNullOrWhiteSpace(projectKey))
        {
            throw new InvalidOperationException("Jira issue creation requires a project key.");
        }

        var client = CreateClient(settings!);
        using var response = await client.PostAsJsonAsync(
            "rest/api/3/issue",
            new
            {
                fields = new
                {
                    project = new { key = projectKey },
                    summary = title,
                    issuetype = new { name = settings!.DefaultIssueType },
                    description = CreateAdfDocument(description),
                    duedate = dueAt?.ToString("yyyy-MM-dd"),
                },
            },
            ct);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var issueKey = document.RootElement.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(issueKey))
        {
            throw new InvalidOperationException("Jira did not return an issue key.");
        }

        return await GetIssueAsync(client, issueKey, settings!, ct);
    }

    /// <summary>
    /// Updates an existing issue in a Jira project.
    /// </summary>
    /// <param name="integration">Integration configuration.</param>
    /// <param name="externalId">External issue identifier.</param>
    /// <param name="status">Optional new status.</param>
    /// <param name="title">Optional new title.</param>
    /// <param name="description">Optional new description.</param>
    /// <param name="dueAt">Optional new due date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated service item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Jira is not configured or issue key is invalid.</exception>
    public async Task<ServiceItem> UpdateItemAsync(
        Integration integration,
        string externalId,
        TaskStatus? status = null,
        string? title = null,
        string? description = null,
        DateTimeOffset? dueAt = null,
        CancellationToken ct = default)
    {
        var settings = await _settingsStore.GetAsync<JiraOptions>(integration.Id, ct);
        if (!IsConfigured(settings))
        {
            throw new InvalidOperationException("Jira is not configured.");
        }

        var client = CreateClient(settings!);

        var fields = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(title))
        {
            fields["summary"] = title;
        }

        if (description is not null)
        {
            fields["description"] = CreateAdfDocument(description);
        }

        if (dueAt is not null)
        {
            fields["duedate"] = dueAt.Value.ToString("yyyy-MM-dd");
        }

        if (fields.Count > 0)
        {
            using var updateResponse = await client.PutAsJsonAsync(
                $"rest/api/3/issue/{Uri.EscapeDataString(externalId)}",
                new { fields },
                ct);
            updateResponse.EnsureSuccessStatusCode();
        }

        if (status == TaskStatus.Done)
        {
            await CloseItemAsync(integration, externalId, ct);
        }

        return await GetIssueAsync(client, externalId, settings!, ct);
    }

    /// <summary>
    /// Fetches a specific issue by its external identifier.
    /// </summary>
    /// <param name="integrationId">Integration identifier.</param>
    /// <param name="externalId">External issue identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Service item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Jira is not configured.</exception>
    public async Task<ServiceItem> FetchItemByExternalIdAsync(Guid integrationId, string externalId, CancellationToken ct = default)
    {
        var settings = await _settingsStore.GetAsync<JiraOptions>(integrationId, ct);
        if (!IsConfigured(settings))
        {
            throw new InvalidOperationException("Jira is not configured.");
        }

        var client = CreateClient(settings!);
        return await GetIssueAsync(client, externalId, settings!, ct);
    }

    /// <summary>
    /// Fetches a tracked item by external identifier that is assigned to the current user.
    /// </summary>
    /// <param name="integrationId">Integration identifier.</param>
    /// <param name="externalId">External issue identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Service item or null if not assigned to current user.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Jira is not configured.</exception>
    public async Task<ServiceItem?> FetchTrackedItemByExternalIdAsync(Guid integrationId, string externalId, CancellationToken ct = default)
    {
        var settings = await _settingsStore.GetAsync<JiraOptions>(integrationId, ct);
        if (!IsConfigured(settings))
        {
            throw new InvalidOperationException("Jira is not configured.");
        }

        var client = CreateClient(settings!);
        var issue = await TryGetIssueElementAsync(client, externalId, ct);
        if (issue is null)
        {
            return null;
        }

        if (!await IsAssignedToCurrentUserAsync(client, issue.Value, ct))
        {
            return null;
        }

        var normalized = NormalizeIssue(issue.Value, settings!);
        var normalizedExternalId = GetString(normalized, "key") ?? externalId;
        return new ServiceItem(ServiceType, normalizedExternalId, normalized.GetRawText());
    }

    /// <summary>
    /// Adds a comment to a Jira issue.
    /// </summary>
    /// <param name="integration">Integration configuration.</param>
    /// <param name="externalId">Issue key.</param>
    /// <param name="body">Comment body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddCommentAsync(Integration integration, string externalId, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException("Comment body is required.");
        }

        var settings = await _settingsStore.GetAsync<JiraOptions>(integration.Id, ct);
        if (!IsConfigured(settings))
        {
            throw new InvalidOperationException("Jira is not configured.");
        }

        var client = CreateClient(settings!);
        var payload = new { body = CreateAdfDocument(body) };
        using var response = await client.PostAsJsonAsync(
            $"rest/api/3/issue/{Uri.EscapeDataString(externalId)}/comment",
            payload,
            ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Closes a Jira issue using an available transition to a done category.
    /// </summary>
    /// <param name="integration">Integration configuration.</param>
    /// <param name="externalId">Issue key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CloseItemAsync(Integration integration, string externalId, CancellationToken ct = default)
    {
        var settings = await _settingsStore.GetAsync<JiraOptions>(integration.Id, ct);
        if (!IsConfigured(settings))
        {
            throw new InvalidOperationException("Jira is not configured.");
        }

        var client = CreateClient(settings!);
        using var transitionsResponse = await client.GetAsync(
            $"rest/api/3/issue/{Uri.EscapeDataString(externalId)}/transitions",
            ct);
        transitionsResponse.EnsureSuccessStatusCode();

        using var transitionsDoc = JsonDocument.Parse(await transitionsResponse.Content.ReadAsStringAsync(ct));
        string? transitionId = null;
        if (transitionsDoc.RootElement.TryGetProperty("transitions", out var transitions)
            && transitions.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in transitions.EnumerateArray())
            {
                if (!t.TryGetProperty("to", out var to) || to.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!to.TryGetProperty("statusCategory", out var category) || category.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var key = category.TryGetProperty("key", out var catKey) ? catKey.GetString() : null;
                if (string.Equals(key, "done", StringComparison.OrdinalIgnoreCase)
                    && t.TryGetProperty("id", out var idProp))
                {
                    transitionId = idProp.GetString();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(transitionId))
            {
                foreach (var t in transitions.EnumerateArray())
                {
                    if (t.TryGetProperty("id", out var fallbackId))
                    {
                        transitionId = fallbackId.GetString();
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(transitionId))
        {
            throw new InvalidOperationException("No applicable transition was found to close this Jira issue.");
        }

        using var closeResponse = await client.PostAsJsonAsync(
            $"rest/api/3/issue/{Uri.EscapeDataString(externalId)}/transitions",
            new { transition = new { id = transitionId } },
            ct);
        closeResponse.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Maps a Jira service item to unified task format.
    /// </summary>
    /// <param name="item">Service item to map.</param>
    /// <param name="projectId">Optional project identifier.</param>
    /// <param name="integrationId">Optional integration identifier.</param>
    /// <returns>Unified task representation.</returns>
    public UnifiedTask MapToUnifiedTask(ServiceItem item, Guid? projectId = null, Guid? integrationId = null)
    {
        using var raw = JsonDocument.Parse(item.RawJson);
        var root = raw.RootElement;

        var title = GetString(root, "summary") ?? "Untitled";
        var description = GetString(root, "description");
        var createdAt = GetDateTimeOffset(root, "created");
        var dueAt = GetDateOnly(root, "duedate");
        var status = MapStatus(root);
        var priority = MapPriority(root);
        var projectKey = GetNestedString(root, "project", "key");
        var browseUrl = GetString(root, "browseUrl") ?? string.Empty;

        var externalRef = new ExternalRef(
            RegisteredServiceType,
            item.ExternalId,
            browseUrl,
            projectKey,
            integrationId);

        return new UnifiedTask(
            title,
            description: description,
            status: status,
            priority: priority,
            dueAt: dueAt,
            externalRef: externalRef,
            createdAt: createdAt,
            projectId: projectId);
    }

    /// <summary>
    /// Maps a unified task to Jira-oriented JSON for service item storage.
    /// </summary>
    /// <param name="task">Unified task to map.</param>
    /// <returns>Service item representation.</returns>
    public ServiceItem MapFromUnifiedTask(UnifiedTask task)
    {
        var rawJson = JsonSerializer.Serialize(new
        {
            summary = task.Title,
            description = task.Description,
            duedate = task.DueAt?.ToString("yyyy-MM-dd"),
        });

        return new ServiceItem(
            RegisteredServiceType,
            task.ExternalRef?.ExternalId ?? task.Id.ToString(),
            rawJson);
    }

    /// <summary>
    /// Creates an HTTP client for Jira API requests.
    /// </summary>
    /// <param name="settings">Jira settings.</param>
    /// <returns>Configured HTTP client.</returns>
    private HttpClient CreateClient(JiraOptions settings)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(EnsureTrailingSlash(settings.BaseUrl));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var authBytes = Encoding.UTF8.GetBytes($"{settings.Email}:{settings.ApiToken}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        return client;
    }

    /// <summary>
    /// Checks if Jira settings are configured.
    /// </summary>
    /// <param name="settings">Jira settings to validate.</param>
    /// <returns>True if settings are valid.</returns>
    private static bool IsConfigured(JiraOptions? settings)
        => settings is not null
           && !string.IsNullOrWhiteSpace(settings.BaseUrl)
           && !string.IsNullOrWhiteSpace(settings.Email)
           && !string.IsNullOrWhiteSpace(settings.ApiToken);

    /// <summary>
    /// Gets an issue from Jira API.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="issueKey">Issue key.</param>
    /// <param name="settings">Jira settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Service item for the issue.</returns>
    /// <exception cref="InvalidOperationException">Thrown when issue is not found.</exception>
    private async Task<ServiceItem> GetIssueAsync(HttpClient client, string issueKey, JiraOptions settings, CancellationToken ct)
    {
        var issue = await GetIssueElementAsync(client, issueKey, ct);
        var normalized = NormalizeIssue(issue, settings);
        var externalId = GetString(normalized, "key") ?? issueKey;
        return new ServiceItem(ServiceType, externalId, normalized.GetRawText());
    }

    /// <summary>
    /// Gets an issue element from Jira API.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="issueKey">Issue key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Issue element data.</returns>
    /// <exception cref="InvalidOperationException">Thrown when issue is not found.</exception>
    private async Task<JsonElement> GetIssueElementAsync(HttpClient client, string issueKey, CancellationToken ct)
    {
        var issue = await TryGetIssueElementAsync(client, issueKey, ct);
        return issue ?? throw new InvalidOperationException("Jira issue was not found.");
    }

    /// <summary>
    /// Tries to get an issue element from Jira API.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="issueKey">Issue key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Issue element data or null if not found.</returns>
    private async Task<JsonElement?> TryGetIssueElementAsync(HttpClient client, string issueKey, CancellationToken ct)
    {
        using var response = await client.GetAsync(
            $"rest/api/3/issue/{Uri.EscapeDataString(issueKey)}?fields={Uri.EscapeDataString(string.Join(",", SearchFields))}",
            ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Tries to fetch issues using the enhanced JQL search endpoint.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="settings">Jira settings.</param>
    /// <param name="jql">JQL query string.</param>
    /// <param name="result">List to append results to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if enhanced search succeeded, false if fallback is needed.</returns>
    private async Task<bool> TryFetchEnhancedSearchAsync(HttpClient client, JiraOptions settings, string jql, List<ServiceItem> result, CancellationToken ct)
    {
        string? nextPageToken = null;

        while (true)
        {
            var payload = new Dictionary<string, object?>
            {
                ["jql"] = jql,
                ["maxResults"] = 100,
                ["fields"] = SearchFields,
            };

            if (!string.IsNullOrWhiteSpace(nextPageToken))
            {
                payload["nextPageToken"] = nextPageToken;
            }

            using var response = await client.PostAsJsonAsync("rest/api/3/search/jql", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                result.Clear();
                return false;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            AppendIssues(document.RootElement, settings, result);

            nextPageToken = document.RootElement.TryGetProperty("nextPageToken", out var nextPageTokenProp)
                ? nextPageTokenProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(nextPageToken))
            {
                return true;
            }
        }
    }

    /// <summary>
    /// Fetches issues using the legacy JQL search endpoint as fallback.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="settings">Jira settings.</param>
    /// <param name="jql">JQL query string.</param>
    /// <param name="result">List to append results to.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task FetchLegacySearchAsync(HttpClient client, JiraOptions settings, string jql, List<ServiceItem> result, CancellationToken ct)
    {
        var startAt = 0;
        while (true)
        {
            using var response = await client.PostAsJsonAsync(
                "rest/api/3/search",
                new
                {
                    jql,
                    startAt,
                    maxResults = 100,
                    fields = SearchFields,
                },
                ct);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var issues = document.RootElement.TryGetProperty("issues", out var issuesProp) && issuesProp.ValueKind == JsonValueKind.Array
                ? issuesProp
                : default;

            if (issues.ValueKind != JsonValueKind.Array || issues.GetArrayLength() == 0)
            {
                break;
            }

            AppendIssues(document.RootElement, settings, result);

            if (issues.GetArrayLength() < 100)
            {
                break;
            }

            startAt += issues.GetArrayLength();
        }
    }

    /// <summary>
    /// Appends issues from search result to the result list.
    /// </summary>
    /// <param name="searchResult">Search result containing issues.</param>
    /// <param name="settings">Jira settings.</param>
    /// <param name="result">List to append issues to.</param>
    private void AppendIssues(JsonElement searchResult, JiraOptions settings, List<ServiceItem> result)
    {
        if (!searchResult.TryGetProperty("issues", out var issues) || issues.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var issue in issues.EnumerateArray())
        {
            var normalized = NormalizeIssue(issue, settings);
            var externalId = GetString(normalized, "key");
            if (string.IsNullOrWhiteSpace(externalId))
            {
                continue;
            }

            result.Add(new ServiceItem(ServiceType, externalId, normalized.GetRawText()));
        }
    }

    /// <summary>
    /// Builds the JQL query string for fetching issues.
    /// </summary>
    /// <param name="settings">Jira settings.</param>
    /// <param name="since">Sync threshold timestamp.</param>
    /// <param name="includeHistoricalAssignees">Whether to include historical assignees.</param>
    /// <returns>JQL query string.</returns>
    private string BuildJql(JiraOptions settings, DateTimeOffset? since, bool includeHistoricalAssignees)
    {
        var baseJql = string.IsNullOrWhiteSpace(settings.Jql)
            ? BuildDefaultJql(settings, includeHistoricalAssignees)
            : settings.Jql.Trim();

        if (since is null)
        {
            return baseJql;
        }

        var updatedFilter = $"updated >= {since.Value.ToLocalTime():yyyy-MM-dd}";
        var orderByIndex = baseJql.IndexOf("order by", StringComparison.OrdinalIgnoreCase);
        if (orderByIndex >= 0)
        {
            return $"{baseJql[..orderByIndex].Trim()} AND {updatedFilter} {baseJql[orderByIndex..]}";
        }

        return $"{baseJql} AND {updatedFilter}";
    }

    /// <summary>
    /// Builds the default JQL query for fetching issues.
    /// </summary>
    /// <param name="settings">Jira settings.</param>
    /// <param name="includeHistoricalAssignees">Whether to include historical assignees.</param>
    /// <returns>Default JQL query string.</returns>
    private static string BuildDefaultJql(JiraOptions settings, bool includeHistoricalAssignees)
    {
        var assigneeScope = includeHistoricalAssignees
            ? "(assignee = currentUser() OR assignee WAS currentUser())"
            : "assignee = currentUser()";

        if (settings.ProjectKeys.Length == 0)
        {
            return $"{assigneeScope} ORDER BY updated DESC";
        }

        var keys = string.Join(", ", settings.ProjectKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => $"{k.Trim()}"));
        return $"project in ({keys}) AND {assigneeScope} ORDER BY updated DESC";
    }

    /// <summary>
    /// Normalizes a Jira issue to a common format.
    /// </summary>
    /// <param name="issue">Issue data.</param>
    /// <param name="settings">Jira settings.</param>
    /// <returns>Normalized issue data.</returns>
    private JsonElement NormalizeIssue(JsonElement issue, JiraOptions settings)
    {
        var fields = issue.TryGetProperty("fields", out var fieldsProp) ? fieldsProp : default;
        var normalized = new
        {
            key = GetString(issue, "key"),
            browseUrl = BuildBrowseUrl(settings, GetString(issue, "key")),
            summary = GetString(fields, "summary"),
            description = ExtractDescription(fields),
            priority = new
            {
                name = GetNestedString(fields, "priority", "name"),
            },
            assignee = new
            {
                accountId = GetNestedString(fields, "assignee", "accountId"),
            },
            created = GetString(fields, "created"),
            updated = GetString(fields, "updated"),
            duedate = GetString(fields, "duedate"),
            project = new
            {
                key = GetNestedString(fields, "project", "key"),
                name = GetNestedString(fields, "project", "name"),
            },
            status = new
            {
                name = GetNestedString(fields, "status", "name"),
                statusCategory = new
                {
                    key = GetNestedStringFromNestedObject(fields, "status", "statusCategory", "key"),
                },
            },
        };

        return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(normalized));
    }

    /// <summary>
    /// Checks if an issue is assigned to the current user.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="issue">Issue data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if issue is assigned to current user.</returns>
    private static async Task<bool> IsAssignedToCurrentUserAsync(HttpClient client, JsonElement issue, CancellationToken ct)
    {
        var assigneeAccountId = GetNestedStringFromNestedObject(issue, "fields", "assignee", "accountId");
        if (string.IsNullOrWhiteSpace(assigneeAccountId))
        {
            return false;
        }

        using var response = await client.GetAsync("rest/api/3/myself", ct);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var currentAccountId = GetString(document.RootElement, "accountId");
        return !string.IsNullOrWhiteSpace(currentAccountId)
            && string.Equals(currentAccountId, assigneeAccountId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the URL has a trailing slash.
    /// </summary>
    /// <param name="value">URL value.</param>
    /// <returns>URL with trailing slash or empty string.</returns>
    private static string EnsureTrailingSlash(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.EndsWith('/') ? value : $"{value}/";

    /// <summary>
    /// Builds the browse URL for a Jira issue.
    /// </summary>
    /// <param name="settings">Jira settings.</param>
    /// <param name="issueKey">Issue key.</param>
    /// <returns>Browse URL or null if invalid.</returns>
    private static string? BuildBrowseUrl(JiraOptions settings, string? issueKey)
    {
        if (string.IsNullOrWhiteSpace(issueKey))
        {
            return null;
        }

        if (!IsConfigured(settings))
        {
            return null;
        }

        var webBaseUrl = string.IsNullOrWhiteSpace(settings.WebUrl)
            ? settings.BaseUrl
            : settings.WebUrl;

        return $"{EnsureTrailingSlash(webBaseUrl)}browse/{issueKey}";
    }

    /// <summary>
    /// Creates an Atlassian Document Format (ADF) document from text.
    /// </summary>
    /// <param name="text">Text to convert.</param>
    /// <returns>ADF document object.</returns>
    private static object CreateAdfDocument(string? text)
    {
        var safeText = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
        return new
        {
            type = "doc",
            version = 1,
            content = new[]
            {
                new
                {
                    type = "paragraph",
                    content = string.IsNullOrWhiteSpace(safeText)
                        ? []
                        : new object[]
                        {
                            new
                            {
                                type = "text",
                                text = safeText,
                            },
                        },
                },
            },
        };
    }

    /// <summary>
    /// Extracts description from Jira issue data.
    /// </summary>
    /// <param name="root">Root JSON element.</param>
    /// <returns>Extracted description or null if not found.</returns>
    private static string? ExtractDescription(JsonElement root)
    {
        if (!root.TryGetProperty("description", out var description))
        {
            return null;
        }

        if (description.ValueKind == JsonValueKind.String)
        {
            return description.GetString();
        }

        var builder = new StringBuilder();
        AppendAdfText(description, builder);
        var text = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    /// <summary>
    /// Appends ADF text from a JSON node to a string builder.
    /// </summary>
    /// <param name="node">JSON node to parse.</param>
    /// <param name="builder">String builder to append to.</param>
    private static void AppendAdfText(JsonElement node, StringBuilder builder)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "text"
                && node.TryGetProperty("text", out var textProp))
            {
                builder.Append(textProp.GetString());
            }

            if (node.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in contentProp.EnumerateArray())
                {
                    AppendAdfText(child, builder);
                }

                if (node.TryGetProperty("type", out var nodeType) && nodeType.GetString() == "paragraph")
                {
                    builder.AppendLine();
                }
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in node.EnumerateArray())
            {
                AppendAdfText(child, builder);
            }
        }
    }

    /// <summary>
    /// Maps Jira status to unified task status.
    /// </summary>
    /// <param name="root">Issue data.</param>
    /// <returns>Unified task status.</returns>
    private static TaskStatus MapStatus(JsonElement root)
    {
        var categoryKey = GetNestedStringFromNestedObject(root, "status", "statusCategory", "key");
        return categoryKey switch
        {
            "done" => TaskStatus.Done,
            "indeterminate" => TaskStatus.InProgress,
            _ => TaskStatus.Open,
        };
    }

    /// <summary>
    /// Maps Jira priority to unified task priority.
    /// </summary>
    /// <param name="root">Issue data.</param>
    /// <returns>Unified task priority or null if not specified.</returns>
    private static TaskPriority? MapPriority(JsonElement root)
    {
        var value = GetNestedString(root, "priority", "name");
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "lowest" or "low" or "minor" or "trivial" or "p4" or "sev4" => TaskPriority.Low,
            "medium" or "normal" or "moderate" or "p3" or "sev3" => TaskPriority.Medium,
            "high" or "major" or "important" or "p2" or "sev2" => TaskPriority.High,
            "highest" or "critical" or "blocker" or "urgent" or "immediate" or "p1" or "p0" or "sev1" => TaskPriority.Critical,
            _ when normalized.Contains("block", StringComparison.Ordinal) => TaskPriority.Critical,
            _ when normalized.Contains("crit", StringComparison.Ordinal) => TaskPriority.Critical,
            _ when normalized.Contains("urgent", StringComparison.Ordinal) => TaskPriority.Critical,
            _ when normalized.Contains("highest", StringComparison.Ordinal) => TaskPriority.Critical,
            _ when normalized.Contains("high", StringComparison.Ordinal) => TaskPriority.High,
            _ when normalized.Contains("medium", StringComparison.Ordinal) => TaskPriority.Medium,
            _ when normalized.Contains("normal", StringComparison.Ordinal) => TaskPriority.Medium,
            _ when normalized.Contains("low", StringComparison.Ordinal) => TaskPriority.Low,
            _ => null,
        };
    }

    /// <summary>
    /// Gets a string value from JSON object.
    /// </summary>
    /// <param name="root">JSON element.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>String value or null if not found.</returns>
    private static string? GetString(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return root.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;
    }

    /// <summary>
    /// Gets a string value from nested object.
    /// </summary>
    /// <param name="root">JSON element.</param>
    /// <param name="propertyName">Property name.</param>
    /// <param name="nestedProperty">Nested property name.</param>
    /// <returns>String value or null if not found.</returns>
    private static string? GetNestedString(JsonElement root, string propertyName, string nestedProperty)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!root.TryGetProperty(propertyName, out var nested) || nested.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(nested, nestedProperty);
    }

    /// <summary>
    /// Gets a string value from nested object property.
    /// </summary>
    /// <param name="root">JSON element.</param>
    /// <param name="propertyName">Property name.</param>
    /// <param name="nestedObjectProperty">Nested object property name.</param>
    /// <param name="nestedProperty">Nested property name.</param>
    /// <returns>String value or null if not found.</returns>
    private static string? GetNestedStringFromNestedObject(JsonElement root, string propertyName, string nestedObjectProperty, string nestedProperty)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!root.TryGetProperty(propertyName, out var nestedObject) || nestedObject.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetNestedString(nestedObject, nestedObjectProperty, nestedProperty);
    }

    /// <summary>
    /// Gets a date-time offset value from JSON object.
    /// </summary>
    /// <param name="root">JSON element.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>Date-time offset value or null if not found.</returns>
    private static DateTimeOffset? GetDateTimeOffset(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return root.TryGetProperty(propertyName, out var prop) && DateTimeOffset.TryParse(prop.GetString(), out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Gets a date-only value from JSON object.
    /// </summary>
    /// <param name="root">JSON element.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>Date-time offset value or null if not found.</returns>
    private static DateTimeOffset? GetDateOnly(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return root.TryGetProperty(propertyName, out var prop) && DateOnly.TryParse(prop.GetString(), out var parsed)
            ? new DateTimeOffset(parsed.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;
    }

    private static bool IsJiraReady(JiraOptions settings)
        => !string.IsNullOrWhiteSpace(settings.BaseUrl)
            && !string.IsNullOrWhiteSpace(settings.Email)
            && !string.IsNullOrWhiteSpace(settings.ApiToken);
}
