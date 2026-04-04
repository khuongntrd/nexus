using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Nexus.Connectors.Core.Abstractions;
using Nexus.Connectors.Core.Exceptions;
using Nexus.Connectors.Core.Models;
using Nexus.Connectors.Core.OAuth;
using Nexus.Connectors.GitHub.Options;
using Nexus.Core.Entities;
using Nexus.Core.Enums;
using Nexus.Core.ValueObjects;
using TaskStatus = Nexus.Core.Enums.TaskStatus;

namespace Nexus.Connectors.GitHub;

/// <summary>
/// Connector implementation for GitHub service integration.
/// </summary>
public sealed class GitHubConnector : IServiceConnector, IOAuthConnector
{
    private readonly IConnectorSettingsStore _settingsStore;
    private readonly ISyncCheckpointRepository _checkpoints;
    private readonly ITokenStore _tokenStore;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubConnector"/> class.
    /// Creates a new GitHub connector instance.
    /// </summary>
    /// <param name="settingsStore">Connector settings store.</param>
    /// <param name="checkpoints">Sync checkpoints repository.</param>
    /// <param name="tokenStore">Token store.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    public GitHubConnector(
        IConnectorSettingsStore settingsStore,
        ISyncCheckpointRepository checkpoints,
        ITokenStore tokenStore,
        IHttpClientFactory httpClientFactory)
    {
        _settingsStore = settingsStore;
        _checkpoints = checkpoints;
        _tokenStore = tokenStore;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Canonical service key for this connector (matches plugin manifest).
    /// </summary>
    public static ServiceType RegisteredServiceType { get; } = new("github");

    /// <summary>
    /// Gets the service type for GitHub integration.
    /// </summary>
    public ServiceType ServiceType => RegisteredServiceType;

    /// <summary>
    /// Gets the display name for GitHub service.
    /// </summary>
    public string DisplayName => "GitHub";

    /// <summary>
    /// Gets the authentication modes supported by GitHub.
    /// </summary>
    public AuthMode[] SupportedAuthModes => new[] { AuthMode.PersonalAccessToken, AuthMode.OAuthPkce };

    /// <summary>
    /// Gets the required OAuth scopes for GitHub API access.
    /// </summary>
    public string[] RequiredScopes => ["repo", "read:user"];

    /// <inheritdoc/>
    public PluginManifest Manifest { get; } = new(
        RegisteredServiceType,
        "GitHub",
        [AuthMode.PersonalAccessToken, AuthMode.OAuthPkce],
        ["repo", "read:user"],
        new SyncProfile(30, SupportsIncrementalSync: true, SupportsManualSync: true, SupportsAutoSync: true),
        [
            new ConfigFieldSchema("DisplayName", "Display Name", "text", IsRequired: true),
            new ConfigFieldSchema("AuthMode", "Authentication Mode", "enum", IsRequired: true),
            new ConfigFieldSchema("PersonalAccessToken", "Personal Access Token", "password", IsRequired: false),
            new ConfigFieldSchema("OAuthClientId", "OAuth Client ID", "text", IsRequired: false),
            new ConfigFieldSchema("OAuthClientSecret", "OAuth Client Secret", "password", IsRequired: false),
            new ConfigFieldSchema("Repositories", "Repositories", "multiline", IsRequired: true)
        ],
        [
            new StatusMapping(TaskStatus.Open, ["open", "todo"]),
            new StatusMapping(TaskStatus.InProgress, ["in_progress"]),
            new StatusMapping(TaskStatus.Done, ["closed", "done"])
        ],
        PullRequestExternalIdMarker: "#pr#");

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
        var settings = await settingsStore.GetAsync<GitHubOptions>(integration.Id, ct) ?? new GitHubOptions();
        settings.DisplayName = integration.DisplayName;
        settings.AuthMode = integration.AuthMode;
        settings.RepositoriesText = string.Join(
            Environment.NewLine,
            settings.Repositories.Where(r => !string.IsNullOrWhiteSpace(r)));

        var hasToken = settings.AuthMode switch
        {
            AuthMode.PersonalAccessToken => !string.IsNullOrWhiteSpace(settings.PersonalAccessToken),
            AuthMode.OAuthPkce => (await tokenStore.GetAsync(integration.Id, ct)) is not null,
            _ => false,
        };

        return new ConnectionSurface(hasToken, IsGitHubReady(settings, hasToken), settings);
    }

    /// <inheritdoc/>
    public Task<ServiceItem?> FetchItemByExternalIdAsync(
        Integration integration,
        string externalId,
        DateTimeOffset? since,
        CancellationToken ct = default)
        => FetchItemByExternalIdAsync(integration.Id, externalId, ct);

    /// <inheritdoc/>
    public string? GetListAccentBadge(ExternalRef? externalRef)
    {
        if (externalRef is null || externalRef.ServiceType != ServiceType)
        {
            return null;
        }

        var project = string.IsNullOrWhiteSpace(externalRef.ProjectKey)
            ? null
            : externalRef.ProjectKey;

        string? number = null;
        if (!string.IsNullOrWhiteSpace(externalRef.ExternalId))
        {
            var parts = externalRef.ExternalId.Split('#', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 3)
            {
                project ??= parts[0];
                number = parts[2];
            }
        }

        if (string.IsNullOrWhiteSpace(project) && string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(number)
            ? project
            : $"{project} #{number}";
    }

    /// <inheritdoc/>
    public bool IsAttachmentProxyUrl(Uri uri)
        => string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith("/user-attachments/assets/", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<string?> GetAttachmentProxyAccessTokenAsync(
        Integration integration,
        IConnectorSettingsStore settingsStore,
        ITokenStore tokenStore,
        CancellationToken ct = default)
    {
        var settings = await settingsStore.GetAsync<GitHubOptions>(integration.Id, ct);
        if (settings is null)
        {
            return null;
        }

        if (settings.AuthMode == AuthMode.PersonalAccessToken)
        {
            return string.IsNullOrWhiteSpace(settings.PersonalAccessToken) ? null : settings.PersonalAccessToken;
        }

        if (settings.AuthMode == AuthMode.OAuthPkce)
        {
            return (await tokenStore.GetAsync(integration.Id, ct))?.AccessToken;
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<string?> GetAccessTokenAsync(
        Integration integration,
        IConnectorSettingsStore settingsStore,
        ITokenStore tokenStore,
        CancellationToken ct = default)
    {
        var settings = await settingsStore.GetAsync<GitHubOptions>(integration.Id, ct);
        if (settings is null)
        {
            return null;
        }

        if (settings.AuthMode == AuthMode.PersonalAccessToken)
        {
            return string.IsNullOrWhiteSpace(settings.PersonalAccessToken) ? null : settings.PersonalAccessToken;
        }

        if (settings.AuthMode == AuthMode.OAuthPkce)
        {
            return (await tokenStore.GetAsync(integration.Id, ct))?.AccessToken;
        }

        return null;
    }

    /// <summary>
    /// Builds the authorization URL for OAuth PKCE flow.
    /// </summary>
    /// <param name="pkce">OAuth PKCE parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Authorization URL.</returns>
    /// <exception cref="NotSupportedException">Thrown when OAuth is not enabled.</exception>
    public async Task<Uri> BuildAuthorizationUrlAsync(OAuthPkceParams pkce, CancellationToken ct = default)
    {
        var settings = await GetSettingsOrThrowAsync(pkce.IntegrationId, ct);
        if (settings.AuthMode != AuthMode.OAuthPkce)
        {
            throw new NotSupportedException("GitHub OAuth flow is enabled only when auth mode is OAuth 2.0.");
        }

        if (string.IsNullOrWhiteSpace(settings.OAuthClientId))
        {
            throw new InvalidOperationException("GitHub OAuth Client ID is required.");
        }

        var scopes = Uri.EscapeDataString(string.Join(" ", RequiredScopes));
        var query = new StringBuilder("https://github.com/login/oauth/authorize");
        query.Append("?client_id=").Append(Uri.EscapeDataString(settings.OAuthClientId));
        query.Append("&redirect_uri=").Append(Uri.EscapeDataString(pkce.RedirectUri));
        query.Append("&scope=").Append(scopes);
        query.Append("&state=").Append(Uri.EscapeDataString(pkce.State));
        query.Append("&code_challenge=").Append(Uri.EscapeDataString(pkce.CodeChallenge));
        query.Append("&code_challenge_method=S256");
        return new Uri(query.ToString());
    }

    /// <summary>
    /// Exchanges authorization code for access token.
    /// </summary>
    /// <param name="code">Authorization code from OAuth flow.</param>
    /// <param name="pkce">OAuth PKCE parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Token set with access token.</returns>
    /// <exception cref="NotSupportedException">Thrown when OAuth is not enabled.</exception>
    /// <exception cref="InvalidOperationException">Thrown when client credentials are missing.</exception>
    public async Task<TokenSet> ExchangeCodeAsync(string code, OAuthPkceParams pkce, CancellationToken ct = default)
    {
        var settings = await GetSettingsOrThrowAsync(pkce.IntegrationId, ct);
        if (settings.AuthMode != AuthMode.OAuthPkce)
        {
            throw new NotSupportedException("GitHub OAuth flow is enabled only when auth mode is OAuth 2.0.");
        }

        if (string.IsNullOrWhiteSpace(settings.OAuthClientId) || string.IsNullOrWhiteSpace(settings.OAuthClientSecret))
        {
            throw new InvalidOperationException("GitHub OAuth Client ID and Client Secret are required.");
        }

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = settings.OAuthClientId,
                ["client_secret"] = settings.OAuthClientSecret,
                ["code"] = code,
                ["redirect_uri"] = pkce.RedirectUri,
                ["state"] = pkce.State,
                ["code_verifier"] = pkce.CodeVerifier,
            }),
        };

        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.UserAgent.ParseAdd("Nexus/1.0");

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var accessToken = GetString(document.RootElement, "access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            var error = GetString(document.RootElement, "error") ?? "unknown_error";
            var description = GetString(document.RootElement, "error_description") ?? "No access token returned.";
            throw new InvalidOperationException($"GitHub OAuth token exchange failed: {error} ({description})");
        }

        return new TokenSet(accessToken, null, null);
    }

    /// <summary>
    /// Refreshes the access token using the refresh token.
    /// </summary>
    /// <param name="current">Current token set.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated token set.</returns>
    /// <exception cref="NotSupportedException">Thrown when token refresh is not supported.</exception>
    public Task<TokenSet> RefreshTokenAsync(TokenSet current, CancellationToken ct = default)
        => throw new NotSupportedException("GitHub uses a Personal Access Token and does not refresh tokens.");

    /// <summary>
    /// Fetches items from configured GitHub repositories.
    /// </summary>
    /// <param name="integration">Integration configuration.</param>
    /// <param name="since">Sync threshold timestamp.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of service items.</returns>
    public async Task<IReadOnlyList<ServiceItem>> FetchItemsAsync(
        Integration integration,
        DateTimeOffset? since,
        CancellationToken ct = default)
    {
        var settings = await _settingsStore.GetAsync<GitHubOptions>(integration.Id, ct);
        if (settings is null || settings.Repositories.Length == 0)
        {
            return Array.Empty<ServiceItem>();
        }

        var accessToken = await GetAccessTokenAsync(integration, settings, ct);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Array.Empty<ServiceItem>();
        }

        var client = CreateClient(accessToken);
        var currentUser = await TryGetCurrentUserLoginAsync(client, ct);
        var result = new List<ServiceItem>();

        var repositories = await ExpandRepositoriesAsync(settings, client, ct);
        foreach (var repo in repositories)
        {
            var page = 1;
            while (true)
            {
                using var response = await client.GetAsync(BuildIssuesUrl(repo, since, page), ct);
                response.EnsureSuccessStatusCode();

                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    break;
                }

                var items = document.RootElement;
                if (items.GetArrayLength() == 0)
                {
                    break;
                }

                foreach (var item in items.EnumerateArray())
                {
                    var isPullRequest = item.TryGetProperty("pull_request", out _);
                    var source = item;

                    if (isPullRequest)
                    {
                        var number = GetInt(item, "number");
                        if (number is null)
                        {
                            continue;
                        }

                        source = await GetPullRequestDetailsAsync(client, repo, number.Value, ct);
                    }

                    if (!ShouldIncludeItem(source, currentUser, isPullRequest))
                    {
                        continue;
                    }

                    var normalized = NormalizeItem(source, repo, isPullRequest, currentUser);
                    var externalId = BuildExternalId(normalized);
                    result.Add(new ServiceItem(ServiceType, externalId, normalized.GetRawText()));
                }

                if (items.GetArrayLength() < 100)
                {
                    break;
                }

                page++;
            }
        }

        await _checkpoints.UpsertAsync(
            new SyncCheckpoint(ServiceType, DateTimeOffset.UtcNow.ToString("O")),
            ct);

        return result;
    }

    /// <summary>
    /// Fetches a specific item by its external identifier.
    /// </summary>
    /// <param name="integrationId">Integration identifier.</param>
    /// <param name="externalId">External item identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Service item or null if not found.</returns>
    public async Task<ServiceItem?> FetchItemByExternalIdAsync(Guid integrationId, string externalId, CancellationToken ct = default)
    {
        var settings = await _settingsStore.GetAsync<GitHubOptions>(integrationId, ct);
        if (settings is null)
        {
            return null;
        }

        var accessToken = await GetAccessTokenAsync(integrationId, settings, ct);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        if (!TryParseExternalId(externalId, out var repo, out var kind, out var number))
        {
            return null;
        }

        var client = CreateClient(accessToken);
        var isPullRequest = string.Equals(kind, "pr", StringComparison.OrdinalIgnoreCase);
        JsonElement source;

        if (isPullRequest)
        {
            source = await GetPullRequestDetailsAsync(client, repo, number, ct);
        }
        else
        {
            using var response = await client.GetAsync($"repos/{repo}/issues/{number}", ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            source = document.RootElement.Clone();
        }

        var currentUser = await TryGetCurrentUserLoginAsync(client, ct);
        var normalized = NormalizeItem(source, repo, isPullRequest, currentUser);
        return new ServiceItem(ServiceType, BuildExternalId(normalized), normalized.GetRawText());
    }

    /// <summary>
    /// Creates a new issue in a GitHub repository.
    /// </summary>
    /// <param name="integration">Integration configuration.</param>
    /// <param name="title">Item title.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="dueAt">Optional due date (unused for GitHub issue create).</param>
    /// <param name="projectKey">Repository in owner/repo format.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Created service item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when project key is missing or GitHub is not configured.</exception>
    public async Task<ServiceItem> CreateItemAsync(
        Integration integration,
        string title,
        string? description = null,
        DateTimeOffset? dueAt = null,
        string? projectKey = null,
        CancellationToken ct = default)
    {
        _ = dueAt;
        if (string.IsNullOrWhiteSpace(projectKey))
        {
            throw new InvalidOperationException("GitHub issue creation requires a repository in owner/repo format.");
        }

        var settings = await _settingsStore.GetAsync<GitHubOptions>(integration.Id, ct)
            ?? throw new InvalidOperationException("GitHub is not configured.");
        var accessToken = await GetRequiredAccessTokenAsync(integration, settings, ct);
        var client = CreateClient(accessToken);
        using var response = await client.PostAsJsonAsync(
            $"repos/{projectKey}/issues",
            new
            {
                title,
                body = description,
            },
            ct);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var normalized = NormalizeItem(
            document.RootElement.Clone(),
            projectKey,
            isPullRequest: false,
            currentUser: await TryGetCurrentUserLoginAsync(client, ct));
        return new ServiceItem(ServiceType, BuildExternalId(normalized), normalized.GetRawText());
    }

    /// <summary>
    /// Updates an existing issue in a GitHub repository.
    /// </summary>
    /// <param name="integration">Integration configuration.</param>
    /// <param name="externalId">External issue identifier.</param>
    /// <param name="status">Optional new status.</param>
    /// <param name="title">Optional new title.</param>
    /// <param name="description">Optional new description.</param>
    /// <param name="dueAt">Optional new due date (unused for GitHub update).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated service item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when external id is invalid or GitHub is not configured.</exception>
    public async Task<ServiceItem> UpdateItemAsync(
        Integration integration,
        string externalId,
        TaskStatus? status = null,
        string? title = null,
        string? description = null,
        DateTimeOffset? dueAt = null,
        CancellationToken ct = default)
    {
        _ = dueAt;
        if (!TryParseExternalId(externalId, out var repo, out _, out var number))
        {
            throw new InvalidOperationException("Invalid GitHub external id.");
        }

        var payload = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(title))
        {
            payload["title"] = title;
        }

        if (description is not null)
        {
            payload["body"] = description;
        }

        if (status is not null)
        {
            payload["state"] = status == TaskStatus.Done ? "closed" : "open";
        }

        var settings = await _settingsStore.GetAsync<GitHubOptions>(integration.Id, ct)
            ?? throw new InvalidOperationException("GitHub is not configured.");
        var accessToken = await GetRequiredAccessTokenAsync(integration, settings, ct);
        var client = CreateClient(accessToken);
        using var response = await client.PatchAsJsonAsync($"repos/{repo}/issues/{number}", payload, ct);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var issueRoot = document.RootElement.Clone();
        var isPullRequest = issueRoot.TryGetProperty("pull_request", out _);
        JsonElement source = issueRoot;

        if (isPullRequest)
        {
            source = await GetPullRequestDetailsAsync(client, repo, number, ct);
        }

        var normalized = NormalizeItem(source, repo, isPullRequest, await TryGetCurrentUserLoginAsync(client, ct));
        return new ServiceItem(ServiceType, BuildExternalId(normalized), normalized.GetRawText());
    }

    /// <summary>
    /// Adds a comment to a GitHub issue or pull request.
    /// </summary>
    /// <param name="integration">Integration configuration.</param>
    /// <param name="externalId">External issue identifier.</param>
    /// <param name="body">Comment body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when comment body is empty or external id is invalid.</exception>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddCommentAsync(Integration integration, string externalId, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException("Comment body is required.");
        }

        if (!TryParseExternalId(externalId, out var repo, out _, out var number))
        {
            throw new InvalidOperationException("Invalid GitHub external id.");
        }

        var settings = await _settingsStore.GetAsync<GitHubOptions>(integration.Id, ct)
            ?? throw new InvalidOperationException("GitHub is not configured.");
        var accessToken = await GetRequiredAccessTokenAsync(integration, settings, ct);
        var client = CreateClient(accessToken);
        using var response = await client.PostAsJsonAsync(
            $"repos/{repo}/issues/{number}/comments",
            new { body },
            ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Closes a GitHub issue or pull request.
    /// </summary>
    /// <param name="integration">Integration configuration.</param>
    /// <param name="externalId">External issue identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when external id is invalid or GitHub is not configured.</exception>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CloseItemAsync(Integration integration, string externalId, CancellationToken ct = default)
    {
        if (!TryParseExternalId(externalId, out var repo, out _, out var number))
        {
            throw new InvalidOperationException("Invalid GitHub external id.");
        }

        var settings = await _settingsStore.GetAsync<GitHubOptions>(integration.Id, ct)
            ?? throw new InvalidOperationException("GitHub is not configured.");
        var accessToken = await GetRequiredAccessTokenAsync(integration, settings, ct);
        var client = CreateClient(accessToken);
        using var response = await client.PatchAsJsonAsync(
            $"repos/{repo}/issues/{number}",
            new { state = "closed" },
            ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Maps a GitHub service item to unified task format.
    /// </summary>
    /// <param name="item">Service item to map.</param>
    /// <param name="projectId">Optional project identifier.</param>
    /// <param name="integrationId">Optional integration identifier.</param>
    /// <returns>Unified task representation.</returns>
    public UnifiedTask MapToUnifiedTask(ServiceItem item, Guid? projectId = null, Guid? integrationId = null)
    {
        using var raw = JsonDocument.Parse(item.RawJson);
        var root = raw.RootElement;

        var title = GetString(root, "title") ?? "Untitled";
        var description = GetString(root, "body");
        var createdAt = GetDateTimeOffset(root, "created_at");
        var dueAt = GetDateTimeOffset(root, "due_on");
        var state = GetString(root, "state");
        var isPullRequest = GetBool(root, "is_pull_request");
        var status = GetStatus(root, state, isPullRequest);

        var externalRef = new ExternalRef(
            RegisteredServiceType,
            item.ExternalId,
            GetString(root, "html_url") ?? string.Empty,
            GetString(root, "repo"),
            integrationId);

        return new UnifiedTask(
            title,
            description: description,
            status: status,
            dueAt: dueAt,
            externalRef: externalRef,
            createdAt: createdAt,
            projectId: projectId);
    }

    /// <summary>
    /// Maps a unified task to GitHub service item format.
    /// </summary>
    /// <param name="task">Unified task to map.</param>
    /// <returns>Service item representation.</returns>
    public ServiceItem MapFromUnifiedTask(UnifiedTask task)
    {
        var rawJson = JsonSerializer.Serialize(new
        {
            title = task.Title,
            body = task.Description,
            state = task.Status == TaskStatus.Done ? "closed" : "open",
            status = task.Status == TaskStatus.Done ? "done" : task.Status == TaskStatus.InProgress ? "inprogress" : "open",
            created_at = task.CreatedAt,
            updated_at = task.UpdatedAt,
            html_url = task.ExternalRef?.Url,
            repo = task.ExternalRef?.ProjectKey,
            is_pull_request = false,
        });

        return new ServiceItem(
            RegisteredServiceType,
            task.ExternalRef?.ExternalId ?? task.Id.ToString(),
            rawJson);
    }

    private static bool IsGitHubReady(GitHubOptions settings, bool hasToken)
        => settings.AuthMode switch
        {
            AuthMode.PersonalAccessToken => !string.IsNullOrWhiteSpace(settings.PersonalAccessToken)
                && settings.Repositories.Any(r => !string.IsNullOrWhiteSpace(r)),
            AuthMode.OAuthPkce => !string.IsNullOrWhiteSpace(settings.OAuthClientId)
                && !string.IsNullOrWhiteSpace(settings.OAuthClientSecret)
                && hasToken
                && settings.Repositories.Any(r => !string.IsNullOrWhiteSpace(r)),
            _ => false,
        };

    private static IEnumerable<string> GetRepositories(GitHubOptions settings)
    {
        return settings.Repositories
            .Select(repo => repo.Trim())
            .Where(repo => !string.IsNullOrWhiteSpace(repo));
    }

    private async Task<IEnumerable<string>> ExpandRepositoriesAsync(GitHubOptions settings, HttpClient client, CancellationToken ct)
    {
        var result = new List<string>();
        foreach (var entry in GetRepositories(settings))
        {
            if (entry.EndsWith("/*", StringComparison.Ordinal))
            {
                var owner = entry[..^2];
                var page = 1;

                // Try organization repos first, fall back to user repos if org not found
                while (true)
                {
                    using var resp = await client.GetAsync($"orgs/{owner}/repos?per_page=100&page={page}", ct);
                    if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        using var resp2 = await client.GetAsync($"users/{owner}/repos?per_page=100&page={page}", ct);
                        if (!resp2.IsSuccessStatusCode)
                        {
                            break;
                        }

                        using var doc2 = JsonDocument.Parse(await resp2.Content.ReadAsStringAsync(ct));
                        if (doc2.RootElement.ValueKind != JsonValueKind.Array)
                        {
                            break;
                        }

                        var arr2 = doc2.RootElement;
                        if (arr2.GetArrayLength() == 0)
                        {
                            break;
                        }

                        foreach (var item in arr2.EnumerateArray())
                        {
                            var full = GetString(item, "full_name");
                            if (!string.IsNullOrWhiteSpace(full))
                            {
                                result.Add(full);
                            }
                        }

                        if (arr2.GetArrayLength() < 100)
                        {
                            break;
                        }

                        page++;
                        continue;
                    }

                    if (!resp.IsSuccessStatusCode)
                    {
                        break;
                    }

                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                    if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    {
                        break;
                    }

                    var arr = doc.RootElement;
                    if (arr.GetArrayLength() == 0)
                    {
                        break;
                    }

                    foreach (var item in arr.EnumerateArray())
                    {
                        var full = GetString(item, "full_name");
                        if (!string.IsNullOrWhiteSpace(full))
                        {
                            result.Add(full);
                        }
                    }

                    if (arr.GetArrayLength() < 100)
                    {
                        break;
                    }

                    page++;
                }
            }
            else
            {
                result.Add(entry);
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private string BuildIssuesUrl(string repo, DateTimeOffset? since, int page)
    {
        var url = $"repos/{repo}/issues?state=all&per_page=100&page={page}";
        if (since is not null)
        {
            url += $"&since={Uri.EscapeDataString(since.Value.ToUniversalTime().ToString("O"))}";
        }

        return url;
    }

    private string BuildExternalId(JsonElement item)
    {
        var repo = GetString(item, "repo") ?? string.Empty;
        var number = GetInt(item, "number")?.ToString() ?? "0";
        var kind = GetBool(item, "is_pull_request") ? "pr" : "issue";
        return $"{repo}#{kind}#{number}";
    }

    /// <summary>
    /// Tries to parse an external identifier into its components.
    /// </summary>
    /// <param name="externalId">External identifier to parse.</param>
    /// <param name="repo">Output repository identifier.</param>
    /// <param name="kind">Output item kind ("issue" or "pr").</param>
    /// <param name="number">Output item number.</param>
    /// <returns>True if parsing succeeded.</returns>
    private bool TryParseExternalId(string externalId, out string repo, out string kind, out int number)
    {
        repo = string.Empty;
        kind = string.Empty;
        number = 0;

        var parts = externalId.Split('#', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[2], out number))
        {
            return false;
        }

        repo = parts[0];
        kind = parts[1];
        return !string.IsNullOrWhiteSpace(repo) && !string.IsNullOrWhiteSpace(kind);
    }

    /// <summary>
    /// Normalizes a GitHub item to a common format.
    /// </summary>
    /// <param name="item">Item data.</param>
    /// <param name="repo">Repository identifier.</param>
    /// <param name="isPullRequest">Whether the item is a pull request.</param>
    /// <param name="currentUser">Current GitHub user login.</param>
    /// <returns>Normalized item data.</returns>
    private JsonElement NormalizeItem(JsonElement item, string repo, bool isPullRequest, string? currentUser)
    {
        var status = GetStatus(item, GetString(item, "state"), isPullRequest, currentUser);

        var normalized = new
        {
            title = GetString(item, "title") ?? "Untitled",
            body = GetString(item, "body"),
            state = GetString(item, "state") ?? "open",
            status = status == TaskStatus.Done ? "done" : status == TaskStatus.InProgress ? "inprogress" : "open",
            created_at = GetString(item, "created_at"),
            updated_at = GetString(item, "updated_at"),
            html_url = GetString(item, "html_url"),
            repo,
            is_pull_request = isPullRequest,
            number = GetInt(item, "number"),
            creator = GetNestedString(item, "user", "login"),
            assignees = GetNestedLogins(item, "assignees").ToArray(),
            requested_reviewers = GetNestedLogins(item, "requested_reviewers").ToArray(),
            created_by_current_user = currentUser is not null && GetNestedString(item, "user", "login") == currentUser,
            assigned_to_current_user = currentUser is not null && GetNestedLogins(item, "assignees").Contains(currentUser),
            review_requested_current_user = currentUser is not null && GetNestedLogins(item, "requested_reviewers").Contains(currentUser),
            draft = GetBool(item, "draft"),
            mergeable_state = GetString(item, "mergeable_state"),
        };

        return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(normalized));
    }

    /// <summary>
    /// Determines if an item should be included in sync.
    /// </summary>
    /// <param name="item">Item data.</param>
    /// <param name="currentUser">Current GitHub user login.</param>
    /// <param name="isPullRequest">Whether the item is a pull request.</param>
    /// <returns>True if item should be included.</returns>
    private bool ShouldIncludeItem(JsonElement item, string? currentUser, bool isPullRequest)
    {
        return true;
    }

    /// <summary>
    /// Gets the task status from GitHub item data.
    /// </summary>
    /// <param name="item">Item data.</param>
    /// <param name="state">Item state string.</param>
    /// <param name="isPullRequest">Whether the item is a pull request.</param>
    /// <param name="currentUser">Current GitHub user login.</param>
    /// <returns>Unified task status.</returns>
    private TaskStatus GetStatus(JsonElement item, string? state, bool isPullRequest, string? currentUser = null)
    {
        if (state == "closed")
        {
            return TaskStatus.Done;
        }

        var creator = GetNestedString(item, "user", "login");
        var assignees = GetNestedLogins(item, "assignees");

        if (isPullRequest)
        {
            return TaskStatus.InProgress;
        }

        return assignees.Contains(currentUser ?? string.Empty) ? TaskStatus.InProgress : TaskStatus.Open;
    }

    /// <summary>
    /// Tries to get the current user's login from GitHub API.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Current user login or null if failed.</returns>
    private async Task<string?> TryGetCurrentUserLoginAsync(HttpClient client, CancellationToken ct)
    {
        try
        {
            using var response = await client.GetAsync("user", ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return GetString(document.RootElement, "login");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets list of logins from nested array of objects or strings.
    /// </summary>
    /// <param name="root">JSON element.</param>
    /// <param name="propertyName">Property name to get.</param>
    /// <returns>List of logins.</returns>
    private IEnumerable<string> GetNestedLogins(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (!root.TryGetProperty(propertyName, out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var logins = new List<string>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var login = item.GetString();
                if (!string.IsNullOrWhiteSpace(login))
                {
                    logins.Add(login);
                }

                continue;
            }

            if (item.ValueKind == JsonValueKind.Object)
            {
                var login = GetString(item, "login");
                if (!string.IsNullOrWhiteSpace(login))
                {
                    logins.Add(login);
                }
            }
        }

        return logins;
    }

    /// <summary>
    /// Gets a string value from nested object.
    /// </summary>
    /// <param name="root">JSON element.</param>
    /// <param name="propertyName">Property name.</param>
    /// <param name="nestedProperty">Nested property name.</param>
    /// <returns>String value or null if not found.</returns>
    private string? GetNestedString(JsonElement root, string propertyName, string nestedProperty)
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
    /// Gets a string value from JSON object.
    /// </summary>
    /// <param name="root">JSON element.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>String value or null if not found.</returns>
    private string? GetString(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return root.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;
    }

    /// <summary>
    /// Gets a boolean value from JSON object.
    /// </summary>
    /// <param name="root">JSON element.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>Boolean value or false if not found.</returns>
    private bool GetBool(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// Gets an integer value from JSON object.
    /// </summary>
    /// <param name="root">JSON element.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>Integer value or null if not found.</returns>
    private int? GetInt(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return root.TryGetProperty(propertyName, out var prop) && prop.TryGetInt32(out var value)
            ? value
            : null;
    }

    /// <summary>
    /// Gets a date-time offset value from JSON object.
    /// </summary>
    /// <param name="root">JSON element.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>Date-time offset value or null if not found.</returns>
    private DateTimeOffset? GetDateTimeOffset(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return root.TryGetProperty(propertyName, out var prop) && DateTimeOffset.TryParse(prop.GetString(), out var parsed)
            ? parsed
            : null;
    }

    private async Task<JsonElement> GetPullRequestDetailsAsync(HttpClient client, string repo, int number, CancellationToken ct)
    {
        using var response = await client.GetAsync($"repos/{repo}/pulls/{number}", ct);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.Clone();
    }

    private async Task<string?> GetAccessTokenAsync(Guid integrationId, GitHubOptions settings, CancellationToken ct)
    {
        if (settings.AuthMode == AuthMode.PersonalAccessToken)
        {
            return string.IsNullOrWhiteSpace(settings.PersonalAccessToken)
                ? null
                : settings.PersonalAccessToken;
        }

        if (settings.AuthMode == AuthMode.OAuthPkce)
        {
            var tokenSet = await _tokenStore.GetAsync(integrationId, ct);
            return tokenSet?.AccessToken;
        }

        return null;
    }

    private async Task<string> GetRequiredAccessTokenAsync(Integration integration, GitHubOptions settings, CancellationToken ct)
    {
        var accessToken = await GetAccessTokenAsync(integration, settings, ct);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            return accessToken;
        }

        if (settings.AuthMode == AuthMode.OAuthPkce)
        {
            throw new ReconnectRequiredException(ServiceType, "GitHub OAuth token is missing. Reconnect is required.");
        }

        throw new InvalidOperationException("GitHub Personal Access Token is missing.");
    }

    private HttpClient CreateClient(string accessToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://api.github.com/");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Nexus/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private async Task<GitHubOptions> GetSettingsOrThrowAsync(Guid? integrationId, CancellationToken ct)
        => integrationId is null
            ? throw new InvalidOperationException("Integration id is required.")
            : await _settingsStore.GetAsync<GitHubOptions>(integrationId.Value, ct)
                ?? throw new InvalidOperationException("GitHub settings are not configured.");

    private async Task<string?> GetAccessTokenAsync(Integration integration, GitHubOptions settings, CancellationToken ct)
        => await GetAccessTokenAsync(integration.Id, settings, ct);
}
