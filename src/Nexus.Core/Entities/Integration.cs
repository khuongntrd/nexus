using Nexus.Core.Enums;
using Nexus.Core.ValueObjects;

namespace Nexus.Core.Entities;

/// <summary>
/// Entity representing an integration with an external service, including authentication details and synchronization state.
/// </summary>
public sealed class Integration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Integration"/> class with the specified parameters.
    /// </summary>
    /// <param name="serviceType">Service type for this integration.</param>
    /// <param name="displayName">Display name for this integration.</param>
    /// <param name="authMode">Authentication mode for this integration.</param>
    /// <param name="isEnabled">Whether the integration is enabled (default: true).</param>
    /// <param name="id">Optional integration identifier (default: new GUID).</param>
    public Integration(
        ServiceType serviceType,
        string displayName,
        AuthMode authMode,
        bool isEnabled = true,
        Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        ServiceType = serviceType;
        DisplayName = displayName;
        AuthMode = authMode;
        IsEnabled = isEnabled;
    }

    /// <summary>Unique identifier for the integration.</summary>
    public Guid Id { get; private set; }

    /// <summary>Service type for this integration.</summary>
    public ServiceType ServiceType { get; private set; }

    /// <summary>Display name for this integration.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Authentication mode for this integration.</summary>
    public AuthMode AuthMode { get; private set; }

    /// <summary>Last synchronization timestamp.</summary>
    public DateTimeOffset? LastSyncAt { get; private set; }

    /// <summary>Whether the integration is enabled.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Encrypted JSON configuration for the integration.</summary>
    public string? ConfigJson { get; private set; }

    /// <summary>Encrypted JSON token for the integration.</summary>
    public string? TokenJson { get; private set; }

    /// <summary>
    /// Records a synchronization event for the integration.
    /// </summary>
    public void RecordSync()
    {
        LastSyncAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets the encrypted JSON token for the integration.
    /// </summary>
    /// <param name="tokenJson">Token JSON or null to remove.</param>
    public void SetTokenJson(string? tokenJson)
    {
        TokenJson = tokenJson;
    }

    /// <summary>
    /// Sets the encrypted JSON configuration for the integration.
    /// </summary>
    /// <param name="configJson">Config JSON or null to remove.</param>
    public void SetConfigJson(string? configJson)
    {
        ConfigJson = configJson;
    }

    /// <summary>
    /// Sets the enabled state for the integration.
    /// </summary>
    /// <param name="isEnabled">Whether the integration should be enabled.</param>
    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }

    /// <summary>
    /// Sets the authentication mode for the integration.
    /// </summary>
    /// <param name="authMode">Authentication mode.</param>
    public void SetAuthMode(AuthMode authMode)
    {
        AuthMode = authMode;
    }

    /// <summary>
    /// Renames the integration.
    /// </summary>
    /// <param name="displayName">New display name.</param>
    /// <exception cref="InvalidOperationException">Thrown when display name is empty.</exception>
    public void Rename(string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName.Trim();
        }
    }

#pragma warning disable SA1201 // EF Core materialization ctor must follow the public API (SA1202).
    private Integration()
    {
    }
#pragma warning restore SA1201
}
