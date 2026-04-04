using Nexus.Application.Sync;
using Quartz;

namespace Nexus.Web.BackgroundJobs;

/// <summary>
/// Background job for automatic task synchronization from external services.
/// </summary>
[DisallowConcurrentExecution]
public sealed class TaskAutoSyncJob(
    ITaskAutoSyncService taskAutoSyncService,
    ILogger<TaskAutoSyncJob> logger) : IJob
{
    private readonly ITaskAutoSyncService _taskAutoSyncService = taskAutoSyncService;
    private readonly ILogger<TaskAutoSyncJob> _logger = logger;

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("Running scheduled task auto-sync.");
        await _taskAutoSyncService.SyncAllAsync(context.CancellationToken);
    }
}
