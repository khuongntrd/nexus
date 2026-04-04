using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Nexus.Application;
using Nexus.Application.Options;
using Nexus.Infrastructure;
using Nexus.Web.BackgroundJobs;
using Nexus.Web.Components;
using Nexus.Web.Components.Connectors;
using Nexus.Web.Endpoints;
using Nexus.Web.Extensions;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

// Application & Infrastructure
builder.Services.AddNexusApplication(builder.Configuration);
builder.Services.AddNexusInfrastructure(builder.Configuration);

builder.Services.AddConnectorServicesFromDependencies();

// Data Protection
var keyRingOptions = builder.Configuration
    .GetSection(KeyRingOptions.SectionName)
    .Get<KeyRingOptions>() ?? new KeyRingOptions();

var dp = builder.Services
    .AddDataProtection()
    .SetApplicationName("Nexus");

if (keyRingOptions.IsFileConfigured)
{
    dp.PersistKeysToFileSystem(new DirectoryInfo(keyRingOptions.FilePath!));
}
else if (keyRingOptions.IsBlobConfigured)
{
    var blobClient = new BlobClient(new Uri(keyRingOptions.BlobUri!), new DefaultAzureCredential());
    dp.PersistKeysToAzureBlobStorage(blobClient);

    if (!string.IsNullOrWhiteSpace(keyRingOptions.KeyVaultKeyUri))
    {
        dp.ProtectKeysWithAzureKeyVault(new Uri(keyRingOptions.KeyVaultKeyUri), new DefaultAzureCredential());
    }
}

// Session (for OAuth PKCE state)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Blazor Server
builder.Services.AddHttpClient();
builder.Services.AddFluentUIComponents();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<IConnectorUiRegistry, ConnectorUiRegistry>();

builder.Services.AddHttpContextAccessor();

var syncOptions = builder.Configuration.GetSection(Nexus.Application.Options.SyncOptions.SectionName)
    .Get<SyncOptions>()
    ?? new SyncOptions();
var appAuthOptions = builder.Configuration.GetSection(AppAuthOptions.SectionName)
    .Get<AppAuthOptions>()
    ?? new AppAuthOptions();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/auth/signout";
        options.AccessDeniedPath = "/login";
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = $"https://login.microsoftonline.com/{appAuthOptions.TenantId}/v2.0";
        options.ClientId = appAuthOptions.ClientId;
        options.ClientSecret = appAuthOptions.ClientSecret;
        options.CallbackPath = appAuthOptions.CallbackPath;
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.UsePkce = true;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.TokenValidationParameters.ValidateIssuer = false;
        options.TokenValidationParameters.NameClaimType = "name";

        // Allow configuring the redirect URIs from an environment variable
        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                var frontend = Environment.GetEnvironmentVariable("HOSTNAME_URL");
                if (!string.IsNullOrWhiteSpace(frontend))
                {
                    var redirectUri = frontend.TrimEnd('/') + appAuthOptions.CallbackPath;
                    context.ProtocolMessage.RedirectUri = redirectUri;
                }

                return Task.CompletedTask;
            },
            OnRedirectToIdentityProviderForSignOut = context =>
            {
                var frontend = Environment.GetEnvironmentVariable("HOSTNAME_URL");
                if (!string.IsNullOrWhiteSpace(frontend))
                {
                    context.ProtocolMessage.PostLogoutRedirectUri = frontend.TrimEnd('/') + "/";
                }

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddQuartz(quartz =>
{
    if (!syncOptions.AutoSyncEnabled)
    {
        return;
    }

    var jobKey = new JobKey("task-auto-sync");
    quartz.AddJob<TaskAutoSyncJob>(options => options.WithIdentity(jobKey));
    quartz.AddTrigger(options =>
    {
        options.ForJob(jobKey)
            .WithIdentity("task-auto-sync-trigger");

        if (syncOptions.RunOnStartup)
        {
            options.StartNow();
        }
        else
        {
            options.StartAt(DateTimeOffset.UtcNow.AddSeconds(syncOptions.AutoSyncIntervalSeconds));
        }

        options.WithSimpleSchedule(schedule => schedule
            .WithInterval(TimeSpan.FromSeconds(syncOptions.AutoSyncIntervalSeconds))
            .RepeatForever());
    });
});
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

var app = builder.Build();

// Run migrations at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Nexus.Infrastructure.Data.NexusDbContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Database migration failed. Ensure the database is available and the connection string is correct.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseSession();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    var isAllowedPath =
        path.StartsWith("/login", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/auth/signin", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/auth/signout", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(appAuthOptions.CallbackPath, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/auth/callback", StringComparison.OrdinalIgnoreCase);

    if (isAllowedPath)
    {
        await next();
        return;
    }

    if (context.User.Identity?.IsAuthenticated != true)
    {
        var returnUrl = $"{context.Request.Path}{context.Request.QueryString}";
        context.Response.Redirect($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return;
    }

    await next();
});

app.MapGet("/auth/signin", async (HttpContext context, string? returnUrl) =>
{
    var target = !string.IsNullOrWhiteSpace(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
        ? returnUrl
        : "/";

    await context.ChallengeAsync(
        OpenIdConnectDefaults.AuthenticationScheme,
        new AuthenticationProperties
        {
            RedirectUri = target,
        });
});

app.MapGet("/auth/signout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignOutAsync(
        OpenIdConnectDefaults.AuthenticationScheme,
        new AuthenticationProperties
        {
            RedirectUri = "/login",
        });
});

// OAuth callback endpoint
app.MapGet("/auth/callback", OAuthCallbackHandler.Handle);
app.MapGet("/media/github-attachment", async (
    string url,
    IConnectorRegistry connectorRegistry,
    IConnectorSettingsStore connectorSettingsStore,
    ITokenStore tokenStore,
    IIntegrationRepository integrationRepository,
    IHttpClientFactory httpClientFactory,
    CancellationToken ct) =>
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var assetUri))
    {
        return Results.BadRequest("Invalid URL.");
    }

    foreach (var integration in await integrationRepository.GetAllAsync(ct))
    {
        if (!integration.IsEnabled || string.IsNullOrWhiteSpace(integration.ConfigJson))
        {
            continue;
        }

        var connector = connectorRegistry.Get(integration.ServiceType);
        if (connector is null || !connector.IsAttachmentProxyUrl(assetUri))
        {
            continue;
        }

        var accessToken = await connector.GetAttachmentProxyAccessTokenAsync(
            integration,
            connectorSettingsStore,
            tokenStore,
            ct);

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            continue;
        }

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Nexus/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("*/*");

        using var response = await client.GetAsync(assetUri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            return Results.StatusCode((int)response.StatusCode);
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(mediaType) || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest("Attachment did not resolve to an image.");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return Results.File(bytes, mediaType);
    }

    return Results.NotFound();
});

// Blazor
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
