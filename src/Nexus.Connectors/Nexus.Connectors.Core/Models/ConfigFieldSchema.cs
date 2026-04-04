namespace Nexus.Connectors.Core.Models;

/// <summary>
/// Schema definition for a configuration field required by a service connector.
/// </summary>
/// <param name="Name">The internal name of the field.</param>
/// <param name="DisplayName">The human-readable display name of the field.</param>
/// <param name="Type">The data type of the field.</param>
/// <param name="IsRequired">Whether the field is required.</param>
public sealed record ConfigFieldSchema(
    string Name,
    string DisplayName,
    string Type,
    bool IsRequired = false);
