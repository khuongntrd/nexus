using Nexus.Web.Components.Widgets;
using static Nexus.Core.Enums.TaskStatus;

namespace Nexus.Web.Components.Pages;

public partial class Home
{
    private readonly List<UnifiedTask> _tasks = [];
    private readonly List<Integration> _integrations = [];

    [Inject]
    private ITaskRepository TaskRepository { get; set; } = default!;

    [Inject]
    private IIntegrationRepository IntegrationRepository { get; set; } = default!;

    [Inject]
    private IConnectorPluginCatalog ConnectorPluginCatalog { get; set; } = default!;

    private int DueTodayCount => _tasks.Count(IsDueToday);

    private int ActiveWorkCount => _tasks.Count(t => t.Status is InProgress or Blocked or Cancelled);

    private int GitHubPullRequestCount => GitHubPullRequests.Count;

    private int ConnectedIntegrationsCount => _integrations.Count(IsConfiguredIntegration);

    private int OverdueCount => _tasks.Count(IsOverdue);

    private int BlockedCount => _tasks.Count(t => t.Status == Blocked);

    private int DoneThisWeekCount => _tasks.Count(t => t.Status == Done && t.UpdatedAt >= StartOfWeekLocal().ToUniversalTime());

    private List<UnifiedTask> GitHubPullRequests => [.. _tasks
        .Where(t =>
        {
            if (t.ExternalRef is not { } reference)
            {
                return false;
            }

            var marker = ConnectorPluginCatalog.Get(reference.ServiceType)?.PullRequestExternalIdMarker;
            return marker is not null
                && (reference.ExternalId?.Contains(marker, StringComparison.OrdinalIgnoreCase) ?? false)
                && t.Status != Done;
        })
        .OrderByDescending(t => t.UpdatedAt)
        .Take(4)];

    private List<UnifiedTask> TodayFocusTasks => [.. _tasks
        .Where(t => t.Status != Done)
        .OrderByDescending(IsOverdue)
        .ThenByDescending(IsDueToday)
        .ThenByDescending(t => GetPriorityRank(t.Priority))
        .ThenBy(t => t.DueAt ?? DateTimeOffset.MaxValue)
        .Take(4)];

    private List<UnifiedTask> SprintTasks => [.. _tasks
        .Where(t => t.DueAt is not null && t.DueAt.Value.ToLocalTime().Date <= DateTime.Now.Date.AddDays(7))
        .OrderBy(t => t.DueAt)];

    private int SprintOpenCount => SprintTasks.Count(t => t.Status != Done);

    private int SprintDoneCount => SprintTasks.Count(t => t.Status == Done);

    private int SprintCompletionPercent => SprintTasks.Count == 0 ? 0 : (int)Math.Round((SprintDoneCount / (double)SprintTasks.Count) * 100);

    private string SprintStatusClass => BlockedCount > 0 ? "status-open" : SprintCompletionPercent >= 70 ? "status-done" : "status-progress";

    private string SprintStatusLabel => BlockedCount > 0 ? "At risk" : SprintCompletionPercent >= 70 ? "Healthy" : "In motion";

    private string SprintNarrative => SprintTasks.Count == 0
        ? "No due-dated sprint items yet. Add due dates to turn the sprint widget into an actual forecast."
        : $"{SprintDoneCount} of {SprintTasks.Count} sprint-scoped items are complete. {(BlockedCount > 0 ? "Blocked work needs attention before the sprint closes." : "The queue is moving without blocked work right now.")}";

    private List<ConnectorHealthItem> ConnectorSummaries => BuildConnectorSummaries();

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        _tasks.AddRange(await TaskRepository.GetAllAsync());
        _integrations.AddRange(await IntegrationRepository.GetAllAsync());
    }

    private static bool IsOverdue(UnifiedTask task)
        => task.DueAt is not null && task.Status != Done && task.DueAt.Value < DateTimeOffset.UtcNow;

    private static bool IsDueToday(UnifiedTask task)
        => task.DueAt is not null
           && task.Status != Done
           && task.DueAt.Value.ToLocalTime().Date == DateTime.Now.Date;

    private static DateTime StartOfWeekLocal()
    {
        var now = DateTime.Now.Date;
        var diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
        return now.AddDays(-diff);
    }

    private static bool IsConfiguredIntegration(Integration integration)
        => integration.IsEnabled && (!string.IsNullOrWhiteSpace(integration.ConfigJson) || !string.IsNullOrWhiteSpace(integration.TokenJson));

    private static int GetPriorityRank(TaskPriority? priority) => priority switch
    {
        TaskPriority.Critical => 4,
        TaskPriority.High => 3,
        TaskPriority.Medium => 2,
        TaskPriority.Low => 1,
        _ => 0,
    };

    private List<ConnectorHealthItem> BuildConnectorSummaries()
    {
        var items = new List<ConnectorHealthItem>();

        foreach (var manifest in ConnectorPluginCatalog.GetAll())
        {
            items.Add(BuildSummary(manifest.ServiceType, manifest.DisplayName));
        }

        return items;
    }

    private ConnectorHealthItem BuildSummary(ServiceType serviceType, string title)
    {
        var integration = _integrations.FirstOrDefault(i => i.ServiceType == serviceType);
        var configured = integration is not null && IsConfiguredIntegration(integration);
        var detail = integration?.LastSyncAt is null
            ? "No sync recorded yet."
            : $"Last sync {integration.LastSyncAt.Value.ToLocalTime():g}";

        return new ConnectorHealthItem(
            title,
            detail,
            configured ? "Configured" : "Needs setup",
            configured ? "status-done" : "status-open");
    }
}
