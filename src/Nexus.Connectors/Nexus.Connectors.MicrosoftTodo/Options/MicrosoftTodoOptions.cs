using System.ComponentModel.DataAnnotations;

namespace Nexus.Connectors.MicrosoftTodo.Options;

/// <summary>
/// Configuration options for Microsoft To-Do connector integration.
/// </summary>
public sealed record MicrosoftTodoOptions
{
    public const string SectionName = "MicrosoftTodo";

    /// <summary>
    /// Display name for the Microsoft To-Do integration.
    /// </summary>
    [Required]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The OAuth Client ID for Microsoft To-Do authentication.
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The Azure AD tenant ID (default: "consumers").
    /// </summary>
    [Required]
    public string TenantId { get; set; } = "consumers";

    /// <summary>
    /// The OAuth Client Secret for Microsoft To-Do authentication.
    /// </summary>
    [Required]
    public string? ClientSecret { get; set; }

    /// <summary>
    /// The OAuth redirect URI for authentication.
    /// </summary>
    [Required]
    public string RedirectUri { get; set; } = string.Empty;
}
