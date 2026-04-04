using Nexus.Connectors.Core.Abstractions;
using Nexus.Connectors.MicrosoftTodo.Options;
using Nexus.Core.Entities;

namespace Nexus.Connectors.MicrosoftTodo.Components;

/// <summary>
/// UI configuration handler for Microsoft To-Do connector settings.
/// </summary>
public sealed class MicrosoftTodoUiConfig : IConnectorUiConfig
{
    /// <inheritdoc/>
    public Type GetSettingsType() => typeof(MicrosoftTodoOptions);

    /// <inheritdoc/>
    public Task<Dictionary<string, object>> LoadSettingsAsync(Integration integration)
    {
        var settings = new Dictionary<string, object>
        {
            ["Settings"] = new MicrosoftTodoOptions
            {
                TenantId = "consumers",
                RedirectUri = "http://localhost:5000/auth/callback",
            },
        };
        return Task.FromResult(settings);
    }

    /// <inheritdoc/>
    public void NormalizeSettings(Dictionary<string, object> settings)
    {
        // No normalization needed for Microsoft To-Do
    }

    /// <inheritdoc/>
    public string GetDisplayName(Dictionary<string, object> settings, string? fallback, string connectorDisplayName)
    {
        if (settings.TryGetValue("Settings", out var obj) && obj is MicrosoftTodoOptions opts)
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
        => !string.IsNullOrWhiteSpace(tokenJson);

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
        => "microsoft-todo";
}
