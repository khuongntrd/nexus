using Nexus.Core.ValueObjects;

namespace Nexus.Connectors.Core.Abstractions;

/// <summary>
/// Interface for storing and retrieving authentication tokens.
/// </summary>
public interface ITokenStore
{
    /// <summary>
    /// Retrieves authentication tokens for a specific integration.
    /// </summary>
    /// <param name="integrationId">The integration ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tokens, or null if not found.</returns>
    Task<TokenSet?> GetAsync(Guid integrationId, CancellationToken ct = default);

    /// <summary>
    /// Saves authentication tokens for a specific integration.
    /// </summary>
    /// <param name="integrationId">The integration ID.</param>
    /// <param name="tokenSet">The tokens to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveAsync(Guid integrationId, TokenSet tokenSet, CancellationToken ct = default);

    /// <summary>
    /// Deletes authentication tokens for a specific integration.
    /// </summary>
    /// <param name="integrationId">The integration ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid integrationId, CancellationToken ct = default);
}
