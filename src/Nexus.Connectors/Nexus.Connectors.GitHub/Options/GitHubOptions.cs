using System.ComponentModel.DataAnnotations;
using Nexus.Core.Enums;

namespace Nexus.Connectors.GitHub.Options;

/// <summary>
/// Configuration options for GitHub connector integration.
/// </summary>
public sealed record GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>
    /// Display name for the GitHub integration.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Authentication mode for GitHub connection.
    /// </summary>
    public AuthMode AuthMode { get; set; } = AuthMode.PersonalAccessToken;

    /// <summary>
    /// Personal access token for GitHub authentication.
    /// </summary>
    public string PersonalAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// OAuth Client ID for GitHub authentication.
    /// </summary>
    public string OAuthClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth Client Secret for GitHub authentication.
    /// </summary>
    public string OAuthClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of repository identifiers (owner/repo format).
    /// </summary>
    public string RepositoriesText { get; set; } = string.Empty;

    /// <summary>
    /// Array of repository identifiers (owner/repo format).
    /// </summary>
    [MinLength(1)]
    public string[] Repositories { get; set; } = [];
}
