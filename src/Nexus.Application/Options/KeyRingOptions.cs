namespace Nexus.Application.Options;

/// <summary>
/// Configuration options for ASP.NET Core Data Protection key storage.
/// </summary>
public sealed record KeyRingOptions
{
    /// <summary>
    /// The name of the configuration section in appsettings.json.
    /// </summary>
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Persist keys to a local directory (e.g. a Docker volume mount).
    /// Example: /var/nexus/dataprotection
    /// Takes priority over BlobUri when both are set.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Persist keys to Azure Blob Storage.
    /// Example: https://{account}.blob.core.windows.net/{container}/keys.xml
    /// Requires Storage Blob Data Contributor on the app's managed identity.
    /// </summary>
    public string? BlobUri { get; set; }

    /// <summary>
    /// Encrypt the key ring at rest using an Azure Key Vault key.
    /// Example: https://{vault}.vault.azure.net/keys/{key-name}
    /// Requires Key Vault Crypto User on the app's managed identity.
    /// Only applies when BlobUri is also set.
    /// </summary>
    public string? KeyVaultKeyUri { get; set; }

    /// <summary>Gets a value indicating whether file-system key persistence is configured.</summary>
    public bool IsFileConfigured => !string.IsNullOrWhiteSpace(FilePath);

    /// <summary>Gets a value indicating whether Azure Blob Storage key persistence is configured.</summary>
    public bool IsBlobConfigured => !string.IsNullOrWhiteSpace(BlobUri);
}
