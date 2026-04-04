using Nexus.Core.ValueObjects;

namespace Nexus.Connectors.Core.Exceptions;

/// <summary>
/// Exception thrown when a connector requires reconnection due to invalid/expired credentials.
/// </summary>
public sealed class ReconnectRequiredException(ServiceType serviceType, string message, Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// The service type that requires reconnection.
    /// </summary>
    public ServiceType ServiceType { get; } = serviceType;
}
