using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyModel;

namespace Nexus.Web.Extensions;

/// <summary>
/// Provides helper extensions for registering connector services.
/// </summary>
public static class ConnectorServiceCollectionExtensions
{
    /// <summary>
    /// Discovers connector assemblies from runtime dependencies and registers connector implementations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddConnectorServicesFromDependencies(this IServiceCollection services)
    {
        var dependencyContext = DependencyContext.Default;

        var connectorAssemblyNames = (dependencyContext?.RuntimeLibraries ?? [])
            .Where(library =>
                library.Name.StartsWith("Nexus.Connectors.", StringComparison.Ordinal)
                && !library.Name.EndsWith(".Core", StringComparison.Ordinal))
            .SelectMany(library =>
            {
                var defaults = dependencyContext is null
                    ? []
                    : library.GetDefaultAssemblyNames(dependencyContext);
                return defaults.Any()
                    ? defaults.Select(name => name.Name)
                    : [library.Name];
            })
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var loadedName = loadedAssembly.GetName().Name;
            if (string.IsNullOrWhiteSpace(loadedName) || !loadedName.StartsWith("Nexus.Connectors.", StringComparison.Ordinal))
            {
                continue;
            }

            if (!connectorAssemblyNames.Contains(loadedName, StringComparer.Ordinal))
            {
                connectorAssemblyNames.Add(loadedName);
            }
        }

        var connectorAssemblies = new List<Assembly>();
        foreach (var assemblyName in connectorAssemblyNames)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                continue;
            }

            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assemblyName));
                connectorAssemblies.Add(assembly);
            }
            catch
            {
                // Ignore optional connectors that are not available in the current build.
            }
        }

        var connectorTypes = connectorAssemblies
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.OfType<Type>();
                }
            })
            .Where(type => typeof(IServiceConnector).IsAssignableFrom(type)
                && type is { IsClass: true, IsAbstract: false, IsPublic: true })
            .Distinct()
            .ToList();

        foreach (var type in connectorTypes)
        {
            var registeredServiceTypeProp = type.GetProperty(
                "RegisteredServiceType",
                BindingFlags.Public | BindingFlags.Static);
            var registeredServiceType = registeredServiceTypeProp?.GetValue(null);
            if (registeredServiceType is not null)
            {
                services.AddKeyedScoped(typeof(IServiceConnector), registeredServiceType, type);
            }

            services.AddScoped(typeof(IServiceConnector), type);
        }

        return services;
    }
}
