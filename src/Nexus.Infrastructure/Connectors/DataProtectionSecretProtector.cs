using Microsoft.AspNetCore.DataProtection;
using Nexus.Core.ValueObjects;

namespace Nexus.Infrastructure.Connectors;

/// <summary>
/// Provides encryption and decryption services for sensitive data using ASP.NET Core data protection.
/// </summary>
/// <param name="dataProtectionProvider">The data protection provider to use for encryption and decryption.</param>
public sealed class DataProtectionSecretProtector(IDataProtectionProvider dataProtectionProvider)
{
    private const string PurposePrefix = "Nexus.Vault";
    private readonly IDataProtectionProvider _dataProtectionProvider = dataProtectionProvider;

    /// <summary>
    /// Encrypts the specified plaintext value for the given service type and category.
    /// </summary>
    /// <param name="serviceType">The service type context for the protector.</param>
    /// <param name="category">The category context for the protector.</param>
    /// <param name="plaintext">The plaintext value to encrypt.</param>
    /// <returns>The encrypted (protected) value.</returns>
    public string Protect(ServiceType serviceType, string category, string plaintext)
    {
        var protector = CreateProtector(serviceType, category);
        return protector.Protect(plaintext);
    }

    /// <summary>
    /// Decrypts the specified protected value for the given service type and category.
    /// </summary>
    /// <param name="serviceType">The service type context for the protector.</param>
    /// <param name="category">The category context for the protector.</param>
    /// <param name="protectedValue">The encrypted (protected) value to decrypt.</param>
    /// <returns>The decrypted (plaintext) value.</returns>
    public string Unprotect(ServiceType serviceType, string category, string protectedValue)
    {
        var protector = CreateProtector(serviceType, category);
        return protector.Unprotect(protectedValue);
    }

    /// <summary>
    /// Creates a data protector for the specified service type and category.
    /// </summary>
    /// <param name="serviceType">The service type context for the protector.</param>
    /// <param name="category">The category context for the protector.</param>
    /// <returns>An <see cref="IDataProtector"/> instance for the given context.</returns>
    private IDataProtector CreateProtector(ServiceType serviceType, string category)
        => _dataProtectionProvider.CreateProtector($"{PurposePrefix}.{serviceType}.{category}");
}
