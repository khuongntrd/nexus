using System.Text.Json;
using Nexus.Application.Repositories;
using Nexus.Connectors.Core.Abstractions;
using Nexus.Core.ValueObjects;

namespace Nexus.Infrastructure.Connectors;

/// <summary>
/// Manages encrypted token storage for service integrations.
/// </summary>
public sealed class EncryptedTokenStore(
    IIntegrationRepository integrations,
    DataProtectionSecretProtector protector) : ITokenStore
{
    private const string PurposePrefix = "Nexus.TokenStore";

    private readonly IIntegrationRepository _integrations = integrations;
    private readonly DataProtectionSecretProtector _protector = protector;

    /// <summary>
    /// Gets an encrypted token for an integration.
    /// </summary>
    /// <param name="integrationId">Integration identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Token set or null if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when integration or stored token is invalid.</exception>
    public async Task<TokenSet?> GetAsync(Guid integrationId, CancellationToken ct = default)
    {
        var integration = await _integrations.GetByIdAsync(integrationId, ct);
        if (integration?.TokenJson is null)
        {
            return null;
        }

        var json = _protector.Unprotect(integration.ServiceType, PurposePrefix, integration.TokenJson);
        return JsonSerializer.Deserialize<TokenSet>(json)
            ?? throw new InvalidOperationException($"Stored token payload for {integration.ServiceType} is invalid.");
    }

    /// <summary>
    /// Saves an encrypted token for an integration.
    /// </summary>
    /// <param name="integrationId">Integration identifier.</param>
    /// <param name="tokenSet">Token set to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when integration was not found.</exception>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SaveAsync(Guid integrationId, TokenSet tokenSet, CancellationToken ct = default)
    {
        var integration = await _integrations.GetByIdAsync(integrationId, ct)
            ?? throw new InvalidOperationException("Integration was not found.");

        var json = JsonSerializer.Serialize(tokenSet);
        var encrypted = _protector.Protect(integration.ServiceType, PurposePrefix, json);
        integration.SetTokenJson(encrypted);
        await _integrations.UpdateAsync(integration, ct);
    }

    /// <summary>
    /// Deletes an encrypted token for an integration.
    /// </summary>
    /// <param name="integrationId">Integration identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteAsync(Guid integrationId, CancellationToken ct = default)
    {
        var integration = await _integrations.GetByIdAsync(integrationId, ct);
        if (integration is not null)
        {
            integration.SetTokenJson(null);
            await _integrations.UpdateAsync(integration, ct);
        }
    }
}
