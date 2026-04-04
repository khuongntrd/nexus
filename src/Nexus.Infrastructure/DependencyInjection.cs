using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.Connectors;
using Nexus.Application.Options;
using Nexus.Application.Repositories;
using Nexus.Application.Sync;
using Nexus.Connectors.Core.Abstractions;
using Nexus.Infrastructure.Connectors;
using Nexus.Infrastructure.Data;
using Nexus.Infrastructure.Data.Repositories;
using Nexus.Infrastructure.Sync;

namespace Nexus.Infrastructure;

/// <summary>
/// Provides static methods for registering Nexus infrastructure services in the dependency injection container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Nexus infrastructure services to the specified service collection, including database context, repositories, connectors, and sync services.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration for retrieving database options.</param>
    /// <returns>The updated service collection with Nexus infrastructure services registered.</returns>
    public static IServiceCollection AddNexusInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        var dbOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
            ?? new DatabaseOptions();

        IDatabaseProvider provider = dbOptions.Provider.ToLowerInvariant() switch
        {
            "sqlserver" or "mssql" => new SqlServerDatabaseProvider(),
            "sqlite" => new SqliteDatabaseProvider(),
            _ => new PostgresDatabaseProvider(),
        };

        services.AddDbContext<NexusDbContext>(options =>
            provider.Configure(options, dbOptions.ConnectionString));

        // Repositories
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IIntegrationRepository, IntegrationRepository>();
        services.AddScoped<ISyncCheckpointRepository, SyncCheckpointRepository>();

        // Connectors
        services.AddScoped<IConnectorRegistry, ConnectorRegistry>();
        services.AddScoped<IConnectorPluginCatalog, ConnectorPluginCatalog>();
        services.AddScoped<IConnectorPluginRegistry, ConnectorPluginRegistry>();
        services.AddScoped<DataProtectionSecretProtector>();
        services.AddScoped<IConnectorSettingsStore, EncryptedConnectorSettingsStore>();
        services.AddScoped<ITaskAutoSyncService, TaskAutoSyncService>();

        // Token store
        services.AddScoped<ITokenStore, EncryptedTokenStore>();

        // HttpClient
        services.AddHttpClient();

        return services;
    }
}
