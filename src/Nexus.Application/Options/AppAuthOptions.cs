using System.ComponentModel.DataAnnotations;

namespace Nexus.Application.Options;

/// <summary>
/// Configuration options for application authentication.
/// </summary>
public sealed record AppAuthOptions
{
    public const string SectionName = "AppAuth";

    /// <summary>
    /// The OAuth Client ID for application authentication.
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The OAuth Client Secret for application authentication.
    /// </summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The Azure AD tenant ID (default: "consumers").
    /// </summary>
    public string TenantId { get; set; } = "consumers";

    /// <summary>
    /// The OAuth callback path for authentication.
    /// </summary>
    public string CallbackPath { get; set; } = "/signin-oidc";
}
