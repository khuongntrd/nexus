using Nexus.Connectors.Core.Abstractions;
using Nexus.Connectors.Jira.Options;
using Nexus.Core.Entities;

namespace Nexus.Connectors.Jira.Components;

/// <summary>
/// UI configuration handler for Jira connector settings.
/// </summary>
public sealed class JiraUiConfig : IConnectorUiConfig
{
    /// <inheritdoc/>
    public Type GetSettingsType() => typeof(JiraOptions);

    /// <inheritdoc/>
    public Task<Dictionary<string, object>> LoadSettingsAsync(Integration integration)
    {
        var settings = new Dictionary<string, object>
        {
            ["Settings"] = new JiraOptions { DefaultIssueType = "Task" },
        };
        return Task.FromResult(settings);
    }

    /// <inheritdoc/>
    public void NormalizeSettings(Dictionary<string, object> settings)
    {
        if (settings.TryGetValue("Settings", out var obj) && obj is JiraOptions opts)
        {
            opts.ProjectKeys = SplitLines(opts.ProjectKeysText);
        }
    }

    /// <inheritdoc/>
    public string GetDisplayName(Dictionary<string, object> settings, string? fallback, string connectorDisplayName)
    {
        if (settings.TryGetValue("Settings", out var obj) && obj is JiraOptions opts)
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
        if (!settings.TryGetValue("Settings", out var obj) || obj is not JiraOptions opts)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(opts.BaseUrl)
            && !string.IsNullOrWhiteSpace(opts.Email)
            && !string.IsNullOrWhiteSpace(opts.ApiToken);
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

        return parameters;
    }

    /// <inheritdoc/>
    public object GetComponentKey(Dictionary<string, object> settings)
        => "jira";

    private static string[] SplitLines(string? value)
        => (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();
}
