using Nexus.Connectors.Core.Abstractions;
using Nexus.Connectors.GitHub.Options;
using Nexus.Core.Entities;
using Nexus.Core.Enums;

namespace Nexus.Connectors.GitHub.Components;

/// <summary>
/// UI configuration handler for GitHub connector settings.
/// </summary>
public sealed class GitHubUiConfig : IConnectorUiConfig
{
    /// <inheritdoc/>
    public Type GetSettingsType() => typeof(GitHubOptions);

    /// <inheritdoc/>
    public Task<Dictionary<string, object>> LoadSettingsAsync(Integration integration)
    {
        var settings = new Dictionary<string, object>
        {
            ["Settings"] = new GitHubOptions { AuthMode = integration.AuthMode },
        };
        return Task.FromResult(settings);
    }

    /// <inheritdoc/>
    public void NormalizeSettings(Dictionary<string, object> settings)
    {
        if (settings.TryGetValue("Settings", out var obj) && obj is GitHubOptions opts)
        {
            opts.Repositories = SplitLines(opts.RepositoriesText);
        }
    }

    /// <inheritdoc/>
    public string GetDisplayName(Dictionary<string, object> settings, string? fallback, string connectorDisplayName)
    {
        if (settings.TryGetValue("Settings", out var obj) && obj is GitHubOptions opts)
        {
            if (!string.IsNullOrWhiteSpace(opts.DisplayName))
            {
                return opts.DisplayName;
            }
        }

        return fallback ?? connectorDisplayName;
    }

    /// <inheritdoc/>
    public bool IsConfigured(Dictionary<string, object> settings, string? tokenJson)
    {
        if (!settings.TryGetValue("Settings", out var obj) || obj is not GitHubOptions opts)
        {
            return false;
        }

        return opts.AuthMode switch
        {
            AuthMode.PersonalAccessToken => !string.IsNullOrWhiteSpace(opts.PersonalAccessToken)
                && opts.Repositories.Any(r => !string.IsNullOrWhiteSpace(r)),
            AuthMode.OAuthPkce => !string.IsNullOrWhiteSpace(opts.OAuthClientId)
                && !string.IsNullOrWhiteSpace(opts.OAuthClientSecret)
                && !string.IsNullOrWhiteSpace(tokenJson)
                && opts.Repositories.Any(r => !string.IsNullOrWhiteSpace(r)),
            _ => false,
        };
    }

    /// <inheritdoc/>
    public IDictionary<string, object> BuildComponentParameters(
        Dictionary<string, object> settings,
        object? onSettingsChanged = null)
    {
        var parameters = new Dictionary<string, object>();

        if (settings.TryGetValue("Settings", out var obj))
        {
            parameters["Settings"] = obj;
        }

        if (onSettingsChanged is not null)
        {
            parameters["AuthModeChanged"] = onSettingsChanged;
        }

        return parameters;
    }

    /// <inheritdoc/>
    public object GetComponentKey(Dictionary<string, object> settings)
    {
        if (settings.TryGetValue("Settings", out var obj) && obj is GitHubOptions opts)
        {
            return $"{GitHubConnector.RegisteredServiceType.Value}:{opts.AuthMode:G}";
        }

        return GitHubConnector.RegisteredServiceType.Value;
    }

    private static string[] SplitLines(string? value)
        => (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();
}
