using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Nexus.Connectors.Core.Abstractions;
using Nexus.Connectors.Core.Models;
using Nexus.Connectors.Core.OAuth;
using Nexus.Connectors.MicrosoftTodo.Options;
using Nexus.Core.Entities;
using Nexus.Core.Enums;
using Nexus.Core.ValueObjects;
using TaskStatus = Nexus.Core.Enums.TaskStatus;

namespace Nexus.Connectors.MicrosoftTodo;

/// <summary>
/// Connector implementation for Microsoft To-Do service integration.
/// </summary>
public sealed class MicrosoftTodoConnector(
    IConnectorSettingsStore settingsStore,
    ISyncCheckpointRepository checkpoints,
    ITokenStore tokenStore,
    IHttpClientFactory httpClientFactory) : IServiceConnector, IOAuthConnector
{
    private readonly IConnectorSettingsStore _settingsStore = settingsStore;
    private readonly ISyncCheckpointRepository _checkpoints = checkpoints;
    private readonly ITokenStore _tokenStore = tokenStore;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    /// <summary>
    /// Canonical service key for this connector (matches plugin manifest).
    /// </summary>
    public static ServiceType RegisteredServiceType { get; } = new("microsofttodo");

    /// <summary>
    /// Gets the service type for Microsoft To-Do integration.
    /// </summary>
    public ServiceType ServiceType => RegisteredServiceType;

    /// <summary>
    /// Gets the display name for Microsoft To-Do service.
    /// </summary>
    public string DisplayName => "Microsoft To-Do";

    /// <summary>
    /// Gets the authentication modes supported by Microsoft To-Do.
    /// </summary>
    public AuthMode[] SupportedAuthModes => [AuthMode.OAuthPkce];

    /// <summary>
    /// Gets the required OAuth scopes for Microsoft To-Do API access.
    /// </summary>
    public string[] RequiredScopes => ["Tasks.ReadWrite", "offline_access"];

    /// <inheritdoc/>
    public PluginManifest Manifest { get; } = new(
        RegisteredServiceType,
        "Microsoft To-Do",
        [AuthMode.OAuthPkce],
        ["Tasks.ReadWrite", "offline_access"],
        new SyncProfile(30, SupportsIncrementalSync: true, SupportsManualSync: true, SupportsAutoSync: true),
        [
            new ConfigFieldSchema("DisplayName", "Display Name", "text", IsRequired: true),
            new ConfigFieldSchema("ClientId", "Client ID", "text", IsRequired: true),
            new ConfigFieldSchema("TenantId", "Tenant ID", "text", IsRequired: true),
            new ConfigFieldSchema("ClientSecret", "Client Secret", "password", IsRequired: true),
            new ConfigFieldSchema("RedirectUri", "Redirect URI", "url", IsRequired: true)
        ],
        [
            new StatusMapping(TaskStatus.Open, ["notStarted"]),
            new StatusMapping(TaskStatus.InProgress, ["inProgress"]),
            new StatusMapping(TaskStatus.Done, ["completed"])
        ]);

    /// <inheritdoc/>
    public bool SupportsRemoteTaskMutation => false;

    /// <inheritdoc/>
    public bool RequiresCustomOAuthRedirectUri => true;

    /// <inheritdoc/>
    public bool ShouldSkipAutoSyncFor(Integration integration)
        => string.IsNullOrWhiteSpace(integration.TokenJson);

    /// <inheritdoc/>
    public async Task<ConnectionSurface> DescribeConnectionAsync(
        Integration integration,
        IConnectorSettingsStore settingsStore,
        ITokenStore tokenStore,
        CancellationToken ct = default)
    {
        _ = tokenStore;
        var hasToken = !string.IsNullOrWhiteSpace(integration.TokenJson);
        var settings = await settingsStore.GetAsync<MicrosoftTodoOptions>(integration.Id, ct)
            ?? new MicrosoftTodoOptions { TenantId = "consumers", RedirectUri = "http://localhost:5000/auth/callback" };
        settings.DisplayName = integration.DisplayName;
        return new ConnectionSurface(hasToken, hasToken, settings);
    }

    /// <inheritdoc/>
    public async Task<ServiceItem?> FetchItemByExternalIdAsync(
        Integration integration,
        string externalId,
        DateTimeOffset? since,
        CancellationToken ct = default)
    {
        var effectiveSince = since
            ?? integration.LastSyncAt
            ?? DateTimeOffset.UtcNow.AddDays(-30);
        var items = await FetchItemsAsync(integration, effectiveSince, ct);
        return items.FirstOrDefault(i => i.ExternalId == externalId);
    }

    /// <inheritdoc/>
    public string? BuildFallbackSourceUrl(string externalId) => BuildWebSourceUrl(externalId);

    /// <inheritdoc/>
    public async Task<string?> ResolveOAuthRedirectUriAsync(
        Guid integrationId,
        string defaultRedirectUri,
        IConnectorSettingsStore settingsStore,
        CancellationToken ct = default)
    {
        _ = defaultRedirectUri;
        var settings = await settingsStore.GetAsync<MicrosoftTodoOptions>(integrationId, ct);
        return string.IsNullOrWhiteSpace(settings?.RedirectUri) ? null : settings.RedirectUri;
    }

    /// <inheritdoc/>
    public async Task<string?> GetAccessTokenAsync(
        Integration integration,
        IConnectorSettingsStore settingsStore,
        ITokenStore tokenStore,
        CancellationToken ct = default)
    {
        _ = settingsStore;
        var tokenSet = await tokenStore.GetAsync(integration.Id, ct);
        return tokenSet?.AccessToken;
    }

    /// <summary>
    /// Builds a personal access token set for Microsoft To-Do.
    /// </summary>
    /// <param name="pat">Personal access token (not supported).</param>
    /// <returns>Token set (not supported).</returns>
    /// <exception cref="NotSupportedException">Thrown when PAT is used.</exception>
    public TokenSet BuildPatTokenSet(string pat)
    {
        // MS To-Do does not support PAT — return a dummy token set
        throw new NotSupportedException("Microsoft To-Do does not support Personal Access Tokens. Use OAuth PKCE.");
    }

    /// <summary>
    /// Builds the authorization URL for OAuth PKCE flow.
    /// </summary>
    /// <param name="pkce">OAuth PKCE parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Authorization URL.</returns>
    public async Task<Uri> BuildAuthorizationUrlAsync(OAuthPkceParams pkce, CancellationToken ct = default)
    {
        var settings = await GetSettingsOrThrowAsync(pkce.IntegrationId, ct);
        var scopes = string.Join(" ", RequiredScopes);
        var url = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize" +
                  $"?client_id={Uri.EscapeDataString(settings.ClientId)}" +
                  $"&response_type=code" +
                  $"&redirect_uri={Uri.EscapeDataString(pkce.RedirectUri)}" +
                  $"&scope={Uri.EscapeDataString(scopes)}" +
                  $"&code_challenge={Uri.EscapeDataString(pkce.CodeChallenge)}" +
                  $"&code_challenge_method=S256" +
                  $"&state={Uri.EscapeDataString(pkce.State)}";
        return new Uri(url);
    }

    /// <summary>
    /// Exchanges authorization code for access token.
    /// </summary>
    /// <param name="code">Authorization code from OAuth flow.</param>
    /// <param name="pkce">OAuth PKCE parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Token set with access and refresh tokens.</returns>
    /// <exception cref="InvalidOperationException">Thrown when client secret is missing or token response is invalid.</exception>
    public async Task<TokenSet> ExchangeCodeAsync(string code, OAuthPkceParams pkce, CancellationToken ct = default)
    {
        var settings = await GetSettingsOrThrowAsync(pkce.IntegrationId, ct);
        var client = _httpClientFactory.CreateClient();
        var clientSecret = settings.ClientSecret ?? throw new InvalidOperationException("Microsoft To-Do client secret must be configured.");
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = settings.ClientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["redirect_uri"] = pkce.RedirectUri,
            ["code_verifier"] = pkce.CodeVerifier,
        };

        var response = await client.PostAsync(
            $"https://login.microsoftonline.com/common/oauth2/v2.0/token",
            new FormUrlEncodedContent(parameters),
            ct);

        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize token response.");

        return new TokenSet(
            tokenResponse.AccessToken,
            tokenResponse.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn));
    }

    /// <summary>
    /// Refreshes the access token using the refresh token.
    /// </summary>
    /// <param name="current">Current token set.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated token set.</returns>
    /// <exception cref="InvalidOperationException">Thrown when refresh token is missing or refresh flow is not supported.</exception>
    public async Task<TokenSet> RefreshTokenAsync(TokenSet current, CancellationToken ct = default)
    {
        if (current.RefreshToken is null)
        {
            throw new InvalidOperationException("No refresh token available.");
        }

        throw new InvalidOperationException("Use integration-bound refresh flow.");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceItem>> FetchItemsAsync(
        Integration integration,
        DateTimeOffset? since,
        CancellationToken ct = default)
    {
        var tokenSet = await _tokenStore.GetAsync(integration.Id, ct)
            ?? throw new InvalidOperationException("Integration has no token. Please connect first.");

        if (tokenSet.ExpiresAt is not null && tokenSet.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(2))
        {
            tokenSet = await RefreshTokenAsync(integration, tokenSet, ct);
            await _tokenStore.SaveAsync(integration.Id, tokenSet, ct);
        }

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenSet.AccessToken);

        var result = new List<ServiceItem>();

        var listsUrl = "me/todo/lists?$top=50";
        while (!string.IsNullOrWhiteSpace(listsUrl))
        {
            var listsResponse = await client.GetAsync(listsUrl, ct);
            listsResponse.EnsureSuccessStatusCode();

            using var listsDoc = JsonDocument.Parse(await listsResponse.Content.ReadAsStringAsync(ct));
            if (!listsDoc.RootElement.TryGetProperty("value", out var lists) || lists.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            foreach (var list in lists.EnumerateArray())
            {
                if (!list.TryGetProperty("id", out var listIdProp))
                {
                    continue;
                }

                var listId = listIdProp.GetString();
                if (string.IsNullOrWhiteSpace(listId))
                {
                    continue;
                }

                var tasksUrl = $"me/todo/lists/{Uri.EscapeDataString(listId)}/tasks?$top=200";
                while (!string.IsNullOrWhiteSpace(tasksUrl))
                {
                    var tasksResponse = await client.GetAsync(tasksUrl, ct);
                    tasksResponse.EnsureSuccessStatusCode();

                    using var tasksDoc = JsonDocument.Parse(await tasksResponse.Content.ReadAsStringAsync(ct));
                    if (!tasksDoc.RootElement.TryGetProperty("value", out var tasks) || tasks.ValueKind != JsonValueKind.Array)
                    {
                        break;
                    }

                    foreach (var task in tasks.EnumerateArray())
                    {
                        if (!ShouldIncludeTask(task, since))
                        {
                            continue;
                        }

                        if (!task.TryGetProperty("id", out var taskIdProp))
                        {
                            continue;
                        }

                        var taskId = taskIdProp.GetString();
                        if (string.IsNullOrWhiteSpace(taskId))
                        {
                            continue;
                        }

                        var externalId = $"{listId}:{taskId}";
                        var rawJson = task.GetRawText();
                        result.Add(new ServiceItem(ServiceType, externalId, rawJson));
                    }

                    tasksUrl = TryGetNextLink(tasksDoc.RootElement);
                }
            }

            listsUrl = TryGetNextLink(listsDoc.RootElement);
        }

        await _checkpoints.UpsertAsync(new SyncCheckpoint(ServiceType, DateTimeOffset.UtcNow.ToString("O")), ct);

        return result;
    }

    /// <summary>
    /// Creates a new item in Microsoft To-Do service.
    /// </summary>
    /// <param name="integration">Integration configuration.</param>
    /// <param name="title">Item title.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="dueAt">Optional due date.</param>
    /// <param name="projectKey">Optional project key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Created service item.</returns>
    /// <exception cref="NotImplementedException">Thrown when create item is not implemented.</exception>
    public Task<ServiceItem> CreateItemAsync(
        Integration integration,
        string title,
        string? description = null,
        DateTimeOffset? dueAt = null,
        string? projectKey = null,
        CancellationToken ct = default)
    {
        _ = title;
        _ = description;
        _ = dueAt;
        _ = projectKey;
        throw new NotImplementedException("Create item will be implemented in Phase 2.");
    }

    /// <summary>
    /// Updates an existing item in Microsoft To-Do service.
    /// </summary>
    /// <param name="integration">Integration configuration.</param>
    /// <param name="externalId">External item identifier.</param>
    /// <param name="status">Optional new status.</param>
    /// <param name="title">Optional new title.</param>
    /// <param name="description">Optional new description.</param>
    /// <param name="dueAt">Optional new due date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated service item.</returns>
    /// <exception cref="NotImplementedException">Thrown when update item is not implemented.</exception>
    public Task<ServiceItem> UpdateItemAsync(
        Integration integration,
        string externalId,
        TaskStatus? status = null,
        string? title = null,
        string? description = null,
        DateTimeOffset? dueAt = null,
        CancellationToken ct = default)
    {
        _ = status;
        _ = title;
        _ = description;
        _ = dueAt;
        throw new NotImplementedException("Update item will be implemented in Phase 2.");
    }

    /// <summary>
    /// Adds a comment to an item in Microsoft To-Do service.
    /// </summary>
    /// <param name="integration">Integration configuration.</param>
    /// <param name="externalId">External item identifier.</param>
    /// <param name="body">Comment body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="NotImplementedException">Thrown when add comment is not implemented.</exception>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task AddCommentAsync(
        Integration integration,
        string externalId,
        string body,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("Add comment will be implemented in Phase 2.");
    }

    /// <summary>
    /// Closes an item in Microsoft To-Do service.
    /// </summary>
    /// <param name="integration">Integration configuration.</param>
    /// <param name="externalId">External item identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="NotImplementedException">Thrown when close item is not implemented.</exception>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task CloseItemAsync(
        Integration integration,
        string externalId,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("Close item will be implemented in Phase 2.");
    }

    /// <summary>
    /// Maps a service item from Microsoft To-Do to a unified task.
    /// </summary>
    /// <param name="item">Service item to map.</param>
    /// <param name="projectId">Optional project identifier.</param>
    /// <param name="integrationId">Optional integration identifier.</param>
    /// <returns>Unified task representation.</returns>
    public UnifiedTask MapToUnifiedTask(ServiceItem item, Guid? projectId = null, Guid? integrationId = null)
    {
        var rawData = JsonSerializer.Deserialize<JsonElement>(item.RawJson);

        var title = rawData.TryGetProperty("title", out var titleProp)
            ? titleProp.GetString() ?? "Untitled"
            : "Untitled";

        var statusStr = rawData.TryGetProperty("status", out var statusProp)
            ? statusProp.GetString()
            : null;

        var status = statusStr == "completed" ? TaskStatus.Done : TaskStatus.Open;

        DateTimeOffset? dueAt = null;
        if (rawData.TryGetProperty("dueDateTime", out var dueProp)
            && dueProp.ValueKind != JsonValueKind.Null)
        {
            if (dueProp.TryGetProperty("dateTime", out var dtProp))
            {
                if (DateTimeOffset.TryParse(dtProp.GetString(), out var parsed))
                {
                    dueAt = parsed;
                }
            }
        }

        DateTimeOffset? createdAt = null;
        if (rawData.TryGetProperty("createdDateTime", out var createdProp)
            && DateTimeOffset.TryParse(createdProp.GetString(), out var createdParsed))
        {
            createdAt = createdParsed;
        }

        var externalRef = new ExternalRef(
            RegisteredServiceType,
            item.ExternalId,
            BuildWebSourceUrl(item.ExternalId) ?? string.Empty,
            null,
            integrationId);

        return new UnifiedTask(
            title,
            status: status,
            priority: MapPriority(rawData),
            dueAt: dueAt,
            externalRef: externalRef,
            createdAt: createdAt,
            projectId: projectId);
    }

    /// <summary>
    /// Maps a unified task to Microsoft To-Do service item format.
    /// </summary>
    /// <param name="task">Unified task to map.</param>
    /// <returns>Service item representation.</returns>
    public ServiceItem MapFromUnifiedTask(UnifiedTask task)
    {
        var rawJson = JsonSerializer.Serialize(new
        {
            title = task.Title,
            status = task.Status == TaskStatus.Done ? "completed" : "notStarted",
            dueDateTime = task.DueAt.HasValue ? new { dateTime = task.DueAt.Value.ToString("o"), timeZone = "UTC" } : null,
        });

        return new ServiceItem(
            RegisteredServiceType,
            task.ExternalRef?.ExternalId ?? task.Id.ToString(),
            rawJson);
    }

    /// <summary>
    /// Tries to get the next page link from JSON response.
    /// </summary>
    /// <param name="root">JSON element to parse.</param>
    /// <returns>Next page URL or null if not found.</returns>
    private static string? TryGetNextLink(JsonElement root)
    {
        if (root.TryGetProperty("@odata.nextLink", out var nextLinkProp))
        {
            return nextLinkProp.GetString();
        }

        return null;
    }

    /// <summary>
    /// Maps task priority from Microsoft To-Do importance to unified task priority.
    /// </summary>
    /// <param name="rawData">Task data.</param>
    /// <returns>Unified task priority or null if not specified.</returns>
    private static TaskPriority? MapPriority(JsonElement rawData)
    {
        var importance = rawData.TryGetProperty("importance", out var importanceProp)
            ? importanceProp.GetString()
            : null;

        return importance?.ToLowerInvariant() switch
        {
            "low" => TaskPriority.Low,
            "normal" => TaskPriority.Medium,
            "high" => TaskPriority.High,
            _ => null,
        };
    }

    /// <summary>
    /// Determines if a task should be included based on sync threshold.
    /// </summary>
    /// <param name="task">Task to check.</param>
    /// <param name="since">Sync threshold timestamp.</param>
    /// <returns>True if task should be included.</returns>
    private static bool ShouldIncludeTask(JsonElement task, DateTimeOffset? since)
    {
        if (since is null)
        {
            return true;
        }

        if (task.TryGetProperty("lastModifiedDateTime", out var modifiedProp) &&
            DateTimeOffset.TryParse(modifiedProp.GetString(), out var modifiedAt))
        {
            return modifiedAt >= since.Value;
        }

        return true;
    }

    /// <summary>
    /// Builds a user-facing task URL for Microsoft To-Do.
    /// </summary>
    /// <param name="externalId">External task identifier.</param>
    /// <returns>HTTPS URL, or null if invalid.</returns>
    private static string? BuildWebSourceUrl(string externalId)
    {
        var parts = externalId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? $"https://to-do.live.com/tasks/id/{parts[1]}/details" : null;
    }

    private async Task<TokenSet> RefreshTokenAsync(Integration integration, TokenSet current, CancellationToken ct)
    {
        var settings = await GetSettingsOrThrowAsync(integration.Id, ct);
        if (current.RefreshToken is null)
        {
            throw new InvalidOperationException("No refresh token available.");
        }

        var client = _httpClientFactory.CreateClient();
        var clientSecret = settings.ClientSecret ?? throw new InvalidOperationException("Microsoft To-Do client secret must be configured.");
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = settings.ClientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = current.RefreshToken,
        };

        var response = await client.PostAsync(
            "https://login.microsoftonline.com/common/oauth2/v2.0/token",
            new FormUrlEncodedContent(parameters),
            ct);

        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize token response.");

        return new TokenSet(
            tokenResponse.AccessToken,
            tokenResponse.RefreshToken ?? current.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn));
    }

    private async Task<MicrosoftTodoOptions> GetSettingsOrThrowAsync(Guid? integrationId, CancellationToken ct = default)
        => integrationId is null
            ? throw new InvalidOperationException("Integration id is required.")
            : await _settingsStore.GetAsync<MicrosoftTodoOptions>(integrationId.Value, ct)
            ?? throw new InvalidOperationException("Microsoft To-Do settings are not configured.");

    private sealed class TokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]

        /// <summary>Access token for API access.</summary>
        public string AccessToken { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]

        /// <summary>Refresh token for token refresh.</summary>
        public string? RefreshToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]

        /// <summary>Token expiration time in seconds.</summary>
        public int ExpiresIn { get; set; }
    }
}
