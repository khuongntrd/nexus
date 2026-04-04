namespace Nexus.Core.Enums;

/// <summary>
/// Represents the authentication mode used to connect to external services.
/// </summary>
public enum AuthMode
{
    /// <summary>
    /// Authentication using a personal access token.
    /// </summary>
    PersonalAccessToken,

    /// <summary>
    /// Authentication using OAuth with Proof Key for Code Exchange (PKCE).
    /// </summary>
    OAuthPkce,
}
