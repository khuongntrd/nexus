using Microsoft.Extensions.Options;
using Nexus.Application.Options;
using Nexus.Application.Repositories;
using Nexus.Connectors.Core.Abstractions;
using Nexus.Connectors.Core.Models;
using Nexus.Connectors.Core.OAuth;
using Nexus.Core.ValueObjects;

namespace Nexus.Web.Endpoints;

/// <summary>
/// Static handler for OAuth 2.0 callback endpoints.
/// </summary>
public static class OAuthCallbackHandler
{
    /// <summary>
    /// Handles the OAuth callback by exchanging the authorization code for tokens, saving them, and performing an initial sync of tasks from the connected service. Then redirects back to settings page.
    /// </summary>
    /// <param name="request">The incoming HTTP request containing the OAuth callback parameters.</param>
    /// <param name="serviceProvider">The service provider for resolving scoped services.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An HTTP result that redirects the user back to the settings page.</returns>
    public static async Task<IResult> Handle(
        HttpRequest request,
        IServiceProvider serviceProvider,
        CancellationToken ct)
    {
        var code = request.Query["code"].ToString();
        var state = request.Query["state"].ToString();
        var error = request.Query["error"].ToString();

        if (!string.IsNullOrEmpty(error))
        {
            return Results.Redirect($"/settings?error={Uri.EscapeDataString(error)}");
        }

        if (string.IsNullOrEmpty(code))
        {
            return Results.BadRequest("Missing authorization code.");
        }

        // Retrieve PKCE params from session
        var session = request.HttpContext.Session;
        var codeVerifier = session.GetString("pkce_code_verifier");
        var sessionState = session.GetString("pkce_state");
        var redirectUri = session.GetString("pkce_redirect_uri");
        var serviceTypeStr = session.GetString("pkce_service_type");
        var integrationIdStr = session.GetString("pkce_integration_id");

        if (codeVerifier is null || sessionState is null || redirectUri is null || serviceTypeStr is null || integrationIdStr is null)
        {
            return Results.BadRequest("OAuth session data not found. Please try connecting again.");
        }

        if (state != sessionState)
        {
            return Results.BadRequest("OAuth state mismatch. Possible CSRF attack.");
        }

        if (!ServiceType.TryParse(serviceTypeStr, out var serviceType))
        {
            return Results.BadRequest("Invalid service type.");
        }

        // Clear PKCE session data
        session.Remove("pkce_code_verifier");
        session.Remove("pkce_state");
        session.Remove("pkce_redirect_uri");
        session.Remove("pkce_service_type");
        session.Remove("pkce_integration_id");

        if (!Guid.TryParse(integrationIdStr, out var integrationId))
        {
            return Results.BadRequest("Invalid integration id.");
        }

        var pkce = new OAuthPkceParams(
            CodeVerifier: codeVerifier,
            CodeChallenge: string.Empty, // Not needed for exchange
            State: state,
            RedirectUri: redirectUri,
            IntegrationId: integrationId);

        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var registry = scopedServices.GetRequiredService<IConnectorRegistry>();
        var connector = registry.Get(serviceType);
        if (connector is null)
        {
            return Results.BadRequest($"No connector registered for service type: {serviceType}.");
        }

        if (connector is not IOAuthConnector oauthConnector)
        {
            return Results.BadRequest($"OAuth is not supported for service type: {serviceType}.");
        }

        var tokenStore = scopedServices.GetRequiredService<ITokenStore>();
        var integrations = scopedServices.GetRequiredService<IIntegrationRepository>();
        var tasks = scopedServices.GetRequiredService<ITaskRepository>();
        var syncOptions = scopedServices.GetRequiredService<IOptions<SyncOptions>>().Value;

        var tokenSet = await oauthConnector.ExchangeCodeAsync(code, pkce, ct);

        await tokenStore.SaveAsync(integrationId, tokenSet, ct);

        var integration = await integrations.GetByIdAsync(integrationId, ct);
        if (integration is not null)
        {
            var since = integration.LastSyncAt ?? DateTimeOffset.UtcNow.AddDays(-syncOptions.DefaultLookbackDays);
            var coreIntegration = new Core.Entities.Integration(
                integration.ServiceType,
                integration.DisplayName,
                integration.AuthMode,
                integration.IsEnabled,
                integration.Id);
            coreIntegration.SetConfigJson(integration.ConfigJson);
            coreIntegration.SetTokenJson(integration.TokenJson);
            var serviceItems = await connector.FetchItemsAsync(coreIntegration, since, ct);

            var syncedAt = DateTimeOffset.UtcNow;
            foreach (var item in serviceItems)
            {
                var mappedTask = connector.MapToUnifiedTask(item, integrationId: integration.Id);
                var existing = await tasks.GetByExternalRefAsync(serviceType, item.ExternalId, integration.Id, ct);

                if (existing is null)
                {
                    mappedTask.MarkSynced(syncedAt);
                    Core.ValueObjects.ExternalRef? mappedExternalRef = mappedTask.ExternalRef is null
                        ? null
                        : new Core.ValueObjects.ExternalRef(
                            mappedTask.ExternalRef.ServiceType,
                            mappedTask.ExternalRef.ExternalId,
                            mappedTask.ExternalRef.Url,
                            mappedTask.ExternalRef.ProjectKey,
                            mappedTask.ExternalRef.IntegrationId);
                    var appTask = new Core.Entities.UnifiedTask(
                        mappedTask.Title,
                        mappedTask.Description,
                        mappedTask.Status,
                        mappedTask.Priority,
                        mappedTask.SyncFromSource,
                        mappedTask.DueAt,
                        mappedExternalRef,
                        mappedTask.CreatedAt,
                        mappedTask.ProjectId);
                    await tasks.AddAsync(appTask, ct);
                }
                else
                {
                    existing.Update(
                        title: mappedTask.Title,
                        description: mappedTask.Description,
                        status: mappedTask.Status,
                        dueAt: mappedTask.DueAt,
                        createdAt: mappedTask.CreatedAt);
                    if (mappedTask.Priority is not null)
                    {
                        existing.SetPriority(mappedTask.Priority.Value);
                    }

                    if (mappedTask.ExternalRef is not null)
                    {
                        existing.SetExternalRef(new Core.ValueObjects.ExternalRef(
                            mappedTask.ExternalRef.ServiceType,
                            mappedTask.ExternalRef.ExternalId,
                            mappedTask.ExternalRef.Url,
                            mappedTask.ExternalRef.ProjectKey,
                            mappedTask.ExternalRef.IntegrationId));
                    }

                    existing.MarkSynced(syncedAt);

                    await tasks.UpdateAsync(existing, ct);
                }
            }

            integration.RecordSync();
            integration.SetEnabled(true);
            await integrations.UpdateAsync(integration, ct);
        }

        return Results.Redirect("/settings");
    }
}
