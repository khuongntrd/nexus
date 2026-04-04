namespace Nexus.Connectors.Core.Models;

/// <summary>
/// Represents the connection surface for a service connector, including credential status, configuration completeness, and typed settings for the connection editor.
/// </summary>
/// <param name="HasCredential">Whether the integration has a usable credential (token or PAT as applicable).</param>
/// <param name="IsConfigurationComplete">Whether required connector settings are present for sync.</param>
/// <param name="Settings">Typed settings object for the connection editor.</param>
public sealed record ConnectionSurface(
    bool HasCredential,
    bool IsConfigurationComplete,
    object Settings);
