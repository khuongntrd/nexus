using System.Text.Json;
using System.Text.Json.Nodes;
using Nexus.Application.Repositories;
using Nexus.Connectors.Core.Abstractions;

namespace Nexus.Infrastructure.Connectors;

/// <summary>
/// Encrypts integration config JSON at rest. Applies a <b>connector-agnostic</b> normalization:
/// if a top-level string property looks like a JSON object that partially duplicates the root document
/// (legacy copy-paste / double-serialization), that object is merged into the root and the property is
/// replaced with the inner value for the same key when present.
/// </summary>
public sealed class EncryptedConnectorSettingsStore(
    IIntegrationRepository integrations,
    DataProtectionSecretProtector protector) : IConnectorSettingsStore
{
    /// <summary>Minimum overlapping keys (case-insensitive) between root and a string-embedded object to treat it as a duplicate settings blob.</summary>
    private const int MinOverlappingKeysForNestedMerge = 2;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IIntegrationRepository _integrations = integrations;
    private readonly DataProtectionSecretProtector _protector = protector;

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(Guid integrationId, CancellationToken ct = default)
        where T : class
    {
        var integration = await _integrations.GetByIdAsync(integrationId, ct);
        if (integration?.ConfigJson is null)
        {
            return null;
        }

        var json = _protector.Unprotect(integration.ServiceType, "config", integration.ConfigJson);
        return json is null ? null : DeserializeSettings<T>(json);
    }

    /// <inheritdoc/>
    public async Task SaveAsync<T>(Guid integrationId, T settings, CancellationToken ct = default)
        where T : class
    {
        var integration = await _integrations.GetByIdAsync(integrationId, ct)
            ?? throw new InvalidOperationException("Integration was not found.");

        var json = PreparePersistedConfigJson(settings);
        var encrypted = _protector.Protect(integration.ServiceType, "config", json);

        integration.SetConfigJson(encrypted);
        integration.SetEnabled(true);
        await _integrations.UpdateAsync(integration, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid integrationId, CancellationToken ct = default)
    {
        var integration = await _integrations.GetByIdAsync(integrationId, ct);
        if (integration is null)
        {
            return;
        }

        integration.SetConfigJson(null);
        await _integrations.UpdateAsync(integration, ct);
    }

    private static T? DeserializeSettings<T>(string json)
        where T : class
    {
        var normalizedJson = TryUnwrapJsonString(json);
        var prepared = NormalizeConnectorConfigJson(normalizedJson);
        return JsonSerializer.Deserialize<T>(prepared, JsonOptions);
    }

    private static string PreparePersistedConfigJson<T>(T settings)
        where T : class
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        return NormalizeConnectorConfigJson(json);
    }

    private static string NormalizeConnectorConfigJson(string json)
    {
        try
        {
            if (JsonNode.Parse(json) is not JsonObject root)
            {
                return json;
            }

            foreach (var prop in root.ToList())
            {
                if (prop.Value is not JsonValue jv || !jv.TryGetValue<string>(out var raw))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var s = raw.Trim();
                if (!LooksLikeJson(s))
                {
                    continue;
                }

                JsonObject? nested;
                try
                {
                    nested = JsonNode.Parse(TryUnwrapJsonString(s)) as JsonObject;
                }
                catch
                {
                    continue;
                }

                if (nested is null || !LooksLikeDuplicateSettingsBlob(root, nested, prop.Key))
                {
                    continue;
                }

                MergeNestedIntoRoot(root, nested);
                if (GetPropertyIgnoreCase(nested, prop.Key) is { } innerSameKey)
                {
                    SetPropertyIgnoreCase(root, prop.Key, innerSameKey.DeepClone());
                }

                return root.ToJsonString(JsonOptions);
            }

            return root.ToJsonString(JsonOptions);
        }
        catch
        {
            return json;
        }
    }

    private static bool LooksLikeDuplicateSettingsBlob(JsonObject root, JsonObject nested, string blobPropertyKey)
    {
        var overlap = 0;
        foreach (var p in nested)
        {
            if (p.Key.Equals(blobPropertyKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (GetPropertyIgnoreCase(root, p.Key) is not null)
            {
                overlap++;
            }
        }

        return overlap >= MinOverlappingKeysForNestedMerge;
    }

    private static void MergeNestedIntoRoot(JsonObject root, JsonObject nested)
    {
        foreach (var (key, nestedVal) in nested)
        {
            if (nestedVal is null)
            {
                continue;
            }

            var rootVal = GetPropertyIgnoreCase(root, key);
            if (rootVal is null)
            {
                SetPropertyIgnoreCase(root, key, nestedVal.DeepClone());
                continue;
            }

            if (rootVal is JsonValue rv && nestedVal is JsonValue nv
                && rv.TryGetValue<string>(out var rs) && nv.TryGetValue<string>(out var ns))
            {
                SetPropertyIgnoreCase(root, key, MergeString(rs, ns));
            }
            else if (rootVal is JsonArray && nestedVal is JsonArray na && na.Count > 0)
            {
                SetPropertyIgnoreCase(root, key, na.DeepClone());
            }
            else
            {
                SetPropertyIgnoreCase(root, key, nestedVal.DeepClone());
            }
        }
    }

    private static string MergeString(string? root, string? nested)
        => string.IsNullOrWhiteSpace(nested) ? root ?? string.Empty : nested;

    private static JsonNode? GetPropertyIgnoreCase(JsonObject o, string name)
    {
        foreach (var p in o)
        {
            if (p.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return p.Value;
            }
        }

        return null;
    }

    private static void SetPropertyIgnoreCase(JsonObject o, string name, JsonNode value)
    {
        string? found = null;
        foreach (var p in o)
        {
            if (p.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                found = p.Key;
                break;
            }
        }

        if (found is not null)
        {
            o[found] = value;
        }
        else
        {
            o[name] = value;
        }
    }

    private static void SetPropertyIgnoreCase(JsonObject o, string name, string value)
    {
        var node = JsonValue.Create(value);
        if (node is not null)
        {
            SetPropertyIgnoreCase(o, name, node);
        }
    }

    private static string TryUnwrapJsonString(string value)
    {
        var trimmed = value.Trim();
        if (!LooksLikeJsonString(trimmed))
        {
            return trimmed;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(trimmed, JsonOptions) ?? trimmed;
        }
        catch
        {
            return trimmed;
        }
    }

    private static bool LooksLikeJson(string value)
        => value.StartsWith('{') || value.StartsWith('[') || LooksLikeJsonString(value);

    private static bool LooksLikeJsonString(string value)
        => value.Length > 1 && value[0] == '"' && value[^1] == '"';
}
