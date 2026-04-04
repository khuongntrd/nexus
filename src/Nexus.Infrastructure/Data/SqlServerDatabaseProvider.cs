using Microsoft.EntityFrameworkCore;

namespace Nexus.Infrastructure.Data;

/// <summary>
/// Database provider implementation for Microsoft SQL Server databases.
/// </summary>
public sealed class SqlServerDatabaseProvider : IDatabaseProvider
{
    /// <summary>
    /// Configures Entity Framework for SQL Server with the specified connection string.
    /// </summary>
    /// <param name="builder">The DbContext options builder.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    public void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        builder.UseSqlServer(connectionString);
    }
}
