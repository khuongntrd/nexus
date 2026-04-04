using System.Reflection;
using Nexus.Application.Connectors;
using Nexus.Connectors.Core.Abstractions;
using Nexus.Core.Entities;
using Nexus.Core.ValueObjects;

namespace Nexus.Infrastructure.Connectors;

/// <summary>
/// Registry that provides connector plugin instances for specific service types.
/// </summary>
public sealed class ConnectorPluginRegistry(
    IConnectorSettingsStore settingsStore,
    IConnectorPluginCatalog catalog) : IConnectorPluginRegistry
{
    private readonly IConnectorSettingsStore _settingsStore = settingsStore;
    private readonly IConnectorPluginCatalog _catalog = catalog;

    /// <summary>
    /// Gets a connector plugin for a specific service type.
    /// </summary>
    /// <param name="serviceType">Service type to get plugin for.</param>
    /// <returns>Connector plugin or null if not found.</returns>
    public IConnectorPlugin? Get(ServiceType serviceType)
    {
        var manifest = _catalog.Get(serviceType);
        if (manifest is null)
        {
            return null;
        }

        return new PerServiceConnectorPlugin(serviceType, _settingsStore, _catalog);
    }

    /// <summary>
    /// Gets all available connector plugins.
    /// </summary>
    /// <returns>List of all connector plugins.</returns>
    public IReadOnlyList<IConnectorPlugin> GetAll()
        => _catalog.GetAll().Select(m => (IConnectorPlugin)new PerServiceConnectorPlugin(m.ServiceType, _settingsStore, _catalog)).ToList();

    private sealed class PerServiceConnectorPlugin(ServiceType serviceType, IConnectorSettingsStore settingsStore, IConnectorPluginCatalog catalog) : IConnectorPlugin
    {
        private readonly IConnectorSettingsStore _settingsStore = settingsStore;
        private readonly IConnectorPluginCatalog _catalog = catalog;

        /// <summary>
        /// Gets the service type for this plugin.
        /// </summary>
        public ServiceType ServiceType { get; } = serviceType;

        /// <summary>
        /// Saves settings for a specific settings type.
        /// </summary>
        /// <typeparam name="T">Settings type.</typeparam>
        /// <param name="integration">Integration configuration.</param>
        /// <param name="settings">Settings to save.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task representing the save operation.</returns>
        public Task SaveSettingsAsync<T>(Integration integration, T settings, CancellationToken ct = default)
            where T : class
            => _settingsStore.SaveAsync(integration.Id, settings, ct);

        /// <summary>
        /// Loads settings for a specific settings type.
        /// </summary>
        /// <typeparam name="T">Settings type.</typeparam>
        /// <param name="integration">Integration configuration.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task with loaded settings or null if not found.</returns>
        public Task<T?> LoadSettingsAsync<T>(Integration integration, CancellationToken ct = default)
            where T : class
            => _settingsStore.GetAsync<T>(integration.Id, ct);

        /// <summary>
        /// Validates settings for a specific settings type.
        /// </summary>
        /// <typeparam name="T">Settings type.</typeparam>
        /// <param name="settings">Settings to validate.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task with validation result.</returns>
        public Task<bool> ValidateSettingsAsync<T>(T settings, CancellationToken ct = default)
            where T : class
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Loads settings for an arbitrary settings type.
        /// </summary>
        /// <param name="integration">Integration configuration.</param>
        /// <param name="settingsType">Settings type to load.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task with loaded settings or null if failed.</returns>
        public async Task<object?> LoadSettingsAsync(Integration integration, Type settingsType, CancellationToken ct = default)
        {
            var method = FindGenericMethodDefinition(typeof(IConnectorSettingsStore), nameof(IConnectorSettingsStore.GetAsync), parameterCount: 2)
                ?.MakeGenericMethod(settingsType);
            if (method is null)
            {
                return null;
            }

            var task = (Task?)method.Invoke(_settingsStore, [integration.Id, ct]);
            if (task is null)
            {
                return null;
            }

            await task.ConfigureAwait(false);
            var resultProperty = task.GetType().GetProperty("Result");
            return resultProperty?.GetValue(task);
        }

        /// <summary>
        /// Saves settings for an arbitrary settings type.
        /// </summary>
        /// <param name="integration">Integration configuration.</param>
        /// <param name="settings">Settings to save.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task representing the save operation.</returns>
        public Task SaveSettingsAsync(Integration integration, object settings, CancellationToken ct = default)
        {
            var settingsType = settings.GetType();
            var method = FindGenericMethodDefinition(typeof(IConnectorSettingsStore), nameof(IConnectorSettingsStore.SaveAsync), parameterCount: 3)
                ?.MakeGenericMethod(settingsType);
            if (method is null)
            {
                return Task.CompletedTask;
            }

            var task = (Task?)method.Invoke(_settingsStore, [integration.Id, settings, ct]);
            return task ?? Task.CompletedTask;
        }

        /// <summary>
        /// Validates settings for an arbitrary settings type.
        /// </summary>
        /// <param name="settings">Settings to validate.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task with validation result.</returns>
        public Task<bool> ValidateSettingsAsync(object settings, CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Resolves a generic method definition from an interface (e.g. SaveAsync&lt;T&gt;).
        /// <see cref="Type.GetMethod(string)"/> returns null for generic methods.
        /// </summary>
        private static MethodInfo? FindGenericMethodDefinition(Type declaringType, string name, int parameterCount)
        {
            foreach (var candidate in declaringType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (candidate.Name == name && candidate.IsGenericMethodDefinition && candidate.GetParameters().Length == parameterCount)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
