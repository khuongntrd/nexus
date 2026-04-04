using Microsoft.EntityFrameworkCore;

namespace Nexus.Infrastructure.Data;

/// <summary>
/// Database provider implementation for SQLite databases.
/// </summary>
public sealed class SqliteDatabaseProvider : IDatabaseProvider
{
    /// <summary>
    /// Configures Entity Framework for SQLite with the specified connection string.
    /// </summary>
    /// <param name="builder">The DbContext options builder.</param>
    /// <param name="connectionString">The SQLite connection string.</param>
    public void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        builder.UseSqlite(connectionString);
    }
}
