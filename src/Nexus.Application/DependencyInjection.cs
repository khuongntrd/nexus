using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.Options;

namespace Nexus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNexusApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SyncOptions>()
            .BindConfiguration(SyncOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}
