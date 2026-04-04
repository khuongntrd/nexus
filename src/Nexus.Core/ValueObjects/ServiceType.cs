namespace Nexus.Core.ValueObjects;

/// <summary>
/// Represents a pluggable external service key.
/// </summary>
public readonly record struct ServiceType
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceType"/> struct with the specified value.
    /// </summary>
    /// <param name="value">Service key value.</param>
    public ServiceType(string value)
    {
        Value = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Service type key cannot be empty.", nameof(value))
            : value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Gets the normalized service key value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Tries to parse a service key.
    /// </summary>
    /// <param name="value">Service key value to parse.</param>
    /// <param name="serviceType">Parsed service type if successful; otherwise, default.</param>
    /// <returns>>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string? value, out ServiceType serviceType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            serviceType = default;
            return false;
        }

        serviceType = new ServiceType(value);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
