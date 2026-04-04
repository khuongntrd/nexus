using Microsoft.EntityFrameworkCore;

namespace Nexus.Infrastructure.Data;

/// <summary>
/// Interface for database provider implementations that configure Entity Framework DbContext options.
/// </summary>
public interface IDatabaseProvider
{
    /// <summary>
    /// Configures the Entity Framework DbContext options for the specified connection string.
    /// </summary>
    /// <param name="builder">The DbContext options builder.</param>
    /// <param name="connectionString">The connection string to use for the database.</param>
    void Configure(DbContextOptionsBuilder builder, string connectionString);
}
