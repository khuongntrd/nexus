namespace Nexus.Core.ValueObjects;

/// <summary>
/// Represents an external reference to a task or resource in an external service.
/// </summary>
public sealed record ExternalRef(

    /// <summary>Service type for this external reference.</summary>
    ServiceType ServiceType,

    /// <summary>External identifier from the service.</summary>
    string ExternalId,

    /// <summary>URL to the external resource.</summary>
    string Url,

    /// <summary>Project key for the external resource.</summary>
    string? ProjectKey,

    /// <summary>Integration identifier associated with this reference.</summary>
    Guid? IntegrationId = null);
