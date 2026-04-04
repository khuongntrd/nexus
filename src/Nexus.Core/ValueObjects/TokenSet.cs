namespace Nexus.Core.ValueObjects;

/// <summary>
/// Value object representing a set of authentication tokens for API access.
/// </summary>
/// <param name="AccessToken">The access token for API authentication.</param>
/// <param name="RefreshToken">The refresh token for obtaining new access tokens.</param>
/// <param name="ExpiresAt">The expiration date and time for the access token.</param>
public sealed record TokenSet(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAt);
