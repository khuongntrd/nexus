using Nexus.Connectors.Core.Abstractions;
using Nexus.Connectors.GitHub;
using Nexus.Connectors.GitHub.Components;
using Nexus.Connectors.Jira;
using Nexus.Connectors.Jira.Components;
using Nexus.Connectors.MicrosoftTodo;
using Nexus.Connectors.MicrosoftTodo.Components;
using Nexus.Core.ValueObjects;

namespace Nexus.Web.Components.Connectors;

/// <summary>
/// Registry for managing UI components for different connector types.
/// </summary>
public sealed class ConnectorUiRegistry(IConnectorPluginCatalog catalog) : IConnectorUiRegistry
{
    private static readonly IReadOnlyDictionary<ServiceType, (Type ComponentType, IConnectorUiConfig Config)> UiDefinitions =
        new Dictionary<ServiceType, (Type, IConnectorUiConfig)>
        {
            [MicrosoftTodoConnector.RegisteredServiceType] = (typeof(MicrosoftTodoConfig), new MicrosoftTodoUiConfig()),
            [GitHubConnector.RegisteredServiceType] = (typeof(GitHubConfig), new GitHubUiConfig()),
            [JiraConnector.RegisteredServiceType] = (typeof(JiraConfig), new JiraUiConfig()),
        };

    private readonly IReadOnlyList<ConnectorUiDefinition> _definitions =
        [.. catalog.GetAll()
            .Select(m => UiDefinitions.TryGetValue(m.ServiceType, out var definition)
                ? new ConnectorUiDefinition(m.ServiceType, m.DisplayName, definition.ComponentType, definition.Config)
                : null)
            .Where(d => d is not null)
            .Cast<ConnectorUiDefinition>()];

    /// <inheritdoc/>
    public ConnectorUiDefinition? Get(ServiceType serviceType)
        => _definitions.FirstOrDefault(definition => definition.ServiceType == serviceType);

    /// <inheritdoc/>
    public IReadOnlyList<ConnectorUiDefinition> GetAll()
        => _definitions;
}
