namespace Nexus.Connectors.Jira.Options;

/// <summary>
/// Configuration options for Jira connector integration.
/// </summary>
public sealed record JiraOptions
{
    public const string SectionName = "Jira";

    /// <summary>
    /// Display name for the Jira integration.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for the Jira API.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for the Jira web interface.
    /// </summary>
    public string WebUrl { get; set; } = string.Empty;

    /// <summary>
    /// Email address for Jira authentication.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// API token for Jira authentication.
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of project keys.
    /// </summary>
    public string ProjectKeysText { get; set; } = string.Empty;

    /// <summary>
    /// Custom JQL query for filtering issues.
    /// </summary>
    public string Jql { get; set; } = string.Empty;

    /// <summary>
    /// Array of project keys for filtering issues.
    /// </summary>
    public string[] ProjectKeys { get; set; } = [];

    /// <summary>
    /// Default issue type for creating new issues.
    /// </summary>
    public string DefaultIssueType { get; set; } = "Task";
}
