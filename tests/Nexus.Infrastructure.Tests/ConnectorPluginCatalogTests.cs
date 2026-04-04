using Nexus.Connectors.Core.Models;
using Nexus.Core.Enums;
using Nexus.Core.ValueObjects;
using Nexus.Infrastructure.Connectors;
using UnifiedTaskStatus = Nexus.Core.Enums.TaskStatus;

namespace Nexus.Infrastructure.Tests;

public class ConnectorPluginCatalogTests
{
    [Fact]
    public void GetAll_ShouldContainKnownConnectorManifests()
    {
        var catalog = CreateCatalog();

        var manifests = catalog.GetAll();

        Assert.NotEmpty(manifests);
        Assert.Contains(manifests, m => m.ServiceType == new ServiceType("microsofttodo"));
        Assert.Contains(manifests, m => m.ServiceType == new ServiceType("github"));
        Assert.Contains(manifests, m => m.ServiceType == new ServiceType("jira"));
    }

    [Fact]
    public void Get_GitHubManifest_ShouldExposeStatusMapSchemaAndSyncProfile()
    {
        var catalog = CreateCatalog();

        var manifest = catalog.Get(new ServiceType("github"));

        Assert.NotNull(manifest);
        Assert.Equal("GitHub", manifest!.DisplayName);
        Assert.True(manifest.SyncProfile.SupportsIncrementalSync);
        Assert.True(manifest.SyncProfile.SupportsManualSync);
        Assert.Contains(manifest.ConfigSchema, field => field.Name == "AuthMode");
        Assert.Equal("#pr#", manifest.PullRequestExternalIdMarker);
        Assert.Contains(manifest.StatusMappings, map => map.UnifiedStatus == UnifiedTaskStatus.Open);
        Assert.Contains(manifest.StatusMappings, map => map.UnifiedStatus == UnifiedTaskStatus.InProgress);
        Assert.Contains(manifest.StatusMappings, map => map.UnifiedStatus == UnifiedTaskStatus.Done);
    }

    private static ConnectorPluginCatalog CreateCatalog()
    {
        var manifests = new[]
        {
            new PluginManifest(
                new ServiceType("microsofttodo"),
                "Microsoft To-Do",
                [AuthMode.OAuthPkce],
                ["Tasks.ReadWrite", "offline_access"],
                new SyncProfile(30, true, true, true),
                [new ConfigFieldSchema("DisplayName", "Display Name", "text", true)],
                [new StatusMapping(UnifiedTaskStatus.Open, ["notStarted"])]),
            new PluginManifest(
                new ServiceType("github"),
                "GitHub",
                [AuthMode.PersonalAccessToken, AuthMode.OAuthPkce],
                ["repo", "read:user"],
                new SyncProfile(30, true, true, true),
                [new ConfigFieldSchema("AuthMode", "Authentication Mode", "enum", true)],
                [
                    new StatusMapping(UnifiedTaskStatus.Open, ["open"]),
                    new StatusMapping(UnifiedTaskStatus.InProgress, ["in_progress"]),
                    new StatusMapping(UnifiedTaskStatus.Done, ["closed"])
                ],
                PullRequestExternalIdMarker: "#pr#"),
            new PluginManifest(
                new ServiceType("jira"),
                "Jira",
                [AuthMode.PersonalAccessToken],
                [],
                new SyncProfile(30, true, true, true),
                [new ConfigFieldSchema("BaseUrl", "Base URL", "url", true)],
                [new StatusMapping(UnifiedTaskStatus.Done, ["done"])]),
        };

        return new ConnectorPluginCatalog(new TestConnectorRegistry(manifests));
    }

    private sealed class TestConnectorRegistry(IReadOnlyList<PluginManifest> manifests) : Nexus.Connectors.Core.Abstractions.IConnectorRegistry
    {
        private readonly IReadOnlyList<PluginManifest> _manifests = manifests;

        public Nexus.Connectors.Core.Abstractions.IServiceConnector? Get(ServiceType type) => null;

        public IReadOnlyList<Nexus.Connectors.Core.Abstractions.IServiceConnector> GetAll()
            => [];

        public PluginManifest? GetManifest(ServiceType type)
            => _manifests.FirstOrDefault(m => m.ServiceType == type);

        public IReadOnlyList<PluginManifest> GetAllManifests()
            => _manifests;
    }
}
