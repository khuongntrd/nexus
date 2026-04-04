using Nexus.Core.ValueObjects;

namespace Nexus.Connectors.Core.OAuth;

/// <summary>
/// Optional OAuth capabilities for connectors that support authorization code flows.
/// </summary>
public interface IOAuthConnector
{
    /// <summary>
    /// Builds an OAuth authorization URL for the connector.
    /// </summary>
    /// <param name="pkce">OAuth PKCE parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Authorization URL.</returns>
    Task<Uri> BuildAuthorizationUrlAsync(OAuthPkceParams pkce, CancellationToken ct = default);

    /// <summary>
    /// Exchanges an OAuth authorization code for tokens.
    /// </summary>
    /// <param name="code">Authorization code.</param>
    /// <param name="pkce">OAuth PKCE parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Issued token set.</returns>
    Task<TokenSet> ExchangeCodeAsync(string code, OAuthPkceParams pkce, CancellationToken ct = default);
}
