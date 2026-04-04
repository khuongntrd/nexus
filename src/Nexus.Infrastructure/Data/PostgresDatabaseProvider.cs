using Microsoft.EntityFrameworkCore;

namespace Nexus.Infrastructure.Data;

/// <summary>
/// Database provider implementation for PostgreSQL databases.
/// </summary>
public sealed class PostgresDatabaseProvider : IDatabaseProvider
{
    /// <summary>
    /// Configures Entity Framework for PostgreSQL with the specified connection string.
    /// </summary>
    /// <param name="builder">The DbContext options builder.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    public void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        builder.UseNpgsql(connectionString);
    }
}
