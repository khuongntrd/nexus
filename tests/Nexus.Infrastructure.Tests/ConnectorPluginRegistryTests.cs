using Nexus.Connectors.Core.Abstractions;
using Nexus.Connectors.Core.Models;
using Nexus.Connectors.GitHub.Options;
using Nexus.Core.Entities;
using Nexus.Core.Enums;
using Nexus.Core.ValueObjects;

namespace Nexus.Infrastructure.Tests;

public class ConnectorPluginRegistryTests
{
    [Fact]
    public async Task SaveAndLoad_GitHubOptions_ViaRegistry()
    {
        // Arrange
        var settingsStore = new InMemorySettingsStore();
        var manifest = new PluginManifest(
            new ServiceType("github"),
            "GitHub",
            [AuthMode.PersonalAccessToken, AuthMode.OAuthPkce],
            [],
            new SyncProfile(30, true, true, true),
            [new ConfigFieldSchema("AuthMode", "Authentication Mode", "enum", true)],
            []);
        var catalog = new Connectors.ConnectorPluginCatalog(new TestConnectorRegistry([manifest]));
        var registry = new Connectors.ConnectorPluginRegistry(settingsStore, catalog);

        var integration = new Integration(new ServiceType("github"), "GH", AuthMode.PersonalAccessToken);

        var opts = new GitHubOptions
        {
            DisplayName = "My GH",
            AuthMode = AuthMode.PersonalAccessToken,
            PersonalAccessToken = "secret",
            Repositories = ["org/repo"],
        };

        var plugin = registry.Get(new ServiceType("github"));
        Assert.NotNull(plugin);

        // Act
        await plugin!.SaveSettingsAsync(integration, opts);

        var loadedGeneric = await plugin.LoadSettingsAsync<GitHubOptions>(integration);
        var loadedNonGeneric = await plugin.LoadSettingsAsync(integration, typeof(GitHubOptions)) as GitHubOptions;

        // Assert
        Assert.NotNull(loadedGeneric);
        Assert.Equal(opts.DisplayName, loadedGeneric!.DisplayName);
        Assert.NotNull(loadedNonGeneric);
        Assert.Equal(opts.PersonalAccessToken, loadedNonGeneric!.PersonalAccessToken);
        Assert.Equal(opts.Repositories.Length, loadedGeneric.Repositories.Length);
    }

    private sealed class InMemorySettingsStore : IConnectorSettingsStore
    {
        private readonly Dictionary<Guid, object> _store = [];

        public Task DeleteAsync(Guid integrationId, CancellationToken ct = default)
        {
            _store.Remove(integrationId);
            return Task.CompletedTask;
        }

        public Task<T?> GetAsync<T>(Guid integrationId, CancellationToken ct = default)
            where T : class
        {
            if (_store.TryGetValue(integrationId, out var value) && value is T t)
            {
                return Task.FromResult<T?>(t);
            }

            return Task.FromResult<T?>(default);
        }

        public Task SaveAsync<T>(Guid integrationId, T settings, CancellationToken ct = default)
            where T : class
        {
            _store[integrationId] = settings!;
            return Task.CompletedTask;
        }
    }

    private sealed class TestConnectorRegistry(IReadOnlyList<PluginManifest> manifests) : IConnectorRegistry
    {
        private readonly IReadOnlyList<PluginManifest> _manifests = manifests;

        public IServiceConnector? Get(ServiceType type) => null;

        public IReadOnlyList<IServiceConnector> GetAll()
            => [];

        public PluginManifest? GetManifest(ServiceType type)
            => _manifests.FirstOrDefault(m => m.ServiceType == type);

        public IReadOnlyList<PluginManifest> GetAllManifests()
            => _manifests;
    }
}
