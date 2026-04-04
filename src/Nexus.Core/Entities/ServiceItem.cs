using Nexus.Core.ValueObjects;

namespace Nexus.Core.Entities;

/// <summary>
/// Entity representing an item fetched from an external service, stored for caching and reference purposes.
/// </summary>
public sealed class ServiceItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceItem"/> class with the specified parameters.
    /// </summary>
    /// <param name="serviceType">Service type for this item.</param>
    /// <param name="externalId">External identifier from the service.</param>
    /// <param name="rawJson">Raw JSON representation of the item.</param>
    public ServiceItem(ServiceType serviceType, string externalId, string rawJson)
    {
        Id = Guid.NewGuid();
        ServiceType = serviceType;
        ExternalId = externalId;
        RawJson = rawJson;
        FetchedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Unique identifier for the service item.</summary>
    public Guid Id { get; private set; }

    /// <summary>Service type for this item.</summary>
    public ServiceType ServiceType { get; private set; }

    /// <summary>External identifier from the service.</summary>
    public string ExternalId { get; private set; } = string.Empty;

    /// <summary>Raw JSON representation of the service item.</summary>
    public string RawJson { get; private set; } = string.Empty;

    /// <summary>Timestamp when the item was fetched.</summary>
    public DateTimeOffset FetchedAt { get; private set; }

    /// <summary>
    /// Updates the raw JSON representation of the service item.
    /// </summary>
    /// <param name="rawJson">New raw JSON representation.</param>
    public void UpdateRawJson(string rawJson)
    {
        RawJson = rawJson;
        FetchedAt = DateTimeOffset.UtcNow;
    }

#pragma warning disable SA1201 // EF Core materialization ctor must follow the public API (SA1202).
    private ServiceItem()
    {
    }
#pragma warning restore SA1201
}
