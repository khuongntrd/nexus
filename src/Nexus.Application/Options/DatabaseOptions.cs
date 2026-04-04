using System.ComponentModel.DataAnnotations;

namespace Nexus.Application.Options;

/// <summary>
/// Configuration options for database connection.
/// </summary>
public sealed record DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// The database provider to use (e.g., "postgres", "sqlite", "sqlserver").
    /// </summary>
    public string Provider { get; set; } = "postgres";

    /// <summary>
    /// The connection string for the database.
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
