namespace Nexus.Connectors.Core.OAuth;

/// <summary>
/// Parameters for OAuth 2.0 with Proof Key for Code Exchange (PKCE) authentication.
/// </summary>
/// <param name="CodeVerifier">The PKCE code verifier for token exchange.</param>
/// <param name="CodeChallenge">The PKCE code challenge for authorization URL.</param>
/// <param name="State">The state parameter for CSRF protection.</param>
/// <param name="RedirectUri">The redirect URI for OAuth callback.</param>
/// <param name="IntegrationId">The optional integration ID.</param>
public sealed record OAuthPkceParams(
    string CodeVerifier,
    string CodeChallenge,
    string State,
    string RedirectUri,
    Guid? IntegrationId = null);
