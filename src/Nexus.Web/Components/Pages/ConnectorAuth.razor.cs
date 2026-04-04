using Nexus.Connectors.Core.OAuth;

namespace Nexus.Web.Components.Pages;

public partial class ConnectorAuth : ComponentBase
{
    private string? _errorMessage;
    private string? _authorizationUrl;
    private bool _redirectAttempted;

    [Parameter]
    public string ServiceType { get; set; } = string.Empty;

    [SupplyParameterFromQuery]
    public Guid? IntegrationId { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

    [Inject]
    private IConnectorRegistry ConnectorRegistry { get; set; } = default!;

    [Inject]
    private IConnectorSettingsStore ConnectorSettingsStore { get; set; } = default!;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        if (!Nexus.Core.ValueObjects.ServiceType.TryParse(ServiceType, out var serviceType))
        {
            _errorMessage = $"Unknown service type: {ServiceType}";
            return;
        }

        var connector = ConnectorRegistry.Get(serviceType);
        if (connector is null)
        {
            _errorMessage = $"No connector registered for: {ServiceType}";
            return;
        }

        if (connector is not IOAuthConnector oauthConnector)
        {
            _errorMessage = $"{serviceType} OAuth is not available for this connector.";
            return;
        }

        if (IntegrationId is null)
        {
            _errorMessage = "Integration id is required.";
            return;
        }

        var httpContext = HttpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            _errorMessage = "HTTP context is not available for OAuth flow.";
            return;
        }

        // Try to get Frontend URL from environment, fall back to request scheme/host
        var frontendUrl = Environment.GetEnvironmentVariable("HOSTNAME_URL");
        if (string.IsNullOrWhiteSpace(frontendUrl))
        {
            frontendUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        }

        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var state = GenerateState();

        httpContext.Session.SetString("pkce_code_verifier", codeVerifier);
        httpContext.Session.SetString("pkce_state", state);
        httpContext.Session.SetString("pkce_service_type", serviceType.ToString());
        httpContext.Session.SetString("pkce_integration_id", IntegrationId.Value.ToString());

        var redirectUri = $"{frontendUrl}/auth/callback";
        if (connector.RequiresCustomOAuthRedirectUri)
        {
            var resolved = await connector.ResolveOAuthRedirectUriAsync(
                IntegrationId.Value,
                redirectUri,
                ConnectorSettingsStore);

            if (string.IsNullOrWhiteSpace(resolved))
            {
                _errorMessage = "Connector OAuth settings are missing. Configure them in Settings first.";
                return;
            }

            redirectUri = resolved;
        }

        httpContext.Session.SetString("pkce_redirect_uri", redirectUri);

        try
        {
            var authUrl = await oauthConnector.BuildAuthorizationUrlAsync(new OAuthPkceParams(
                CodeVerifier: codeVerifier,
                CodeChallenge: codeChallenge,
                State: state,
                RedirectUri: redirectUri,
                IntegrationId: IntegrationId.Value));

            _authorizationUrl = authUrl.ToString();
        }
        catch (NotSupportedException)
        {
            _errorMessage = $"{serviceType} OAuth is not available for this connector.";
        }
        catch (InvalidOperationException ex)
        {
            _errorMessage = ex.Message;
        }
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (_redirectAttempted || string.IsNullOrWhiteSpace(_authorizationUrl))
        {
            return;
        }

        _redirectAttempted = true;
        Navigation.NavigateTo(_authorizationUrl, forceLoad: true);
    }

    private void GoToSettings() => Navigation.NavigateTo("/settings");

    private string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(codeVerifier);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Base64UrlEncode(hash);
    }

    private string GenerateState()
    {
        var bytes = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
