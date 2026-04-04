using Microsoft.EntityFrameworkCore;
using Nexus.Core.Entities;
using Nexus.Infrastructure.Data.Configurations;

namespace Nexus.Infrastructure.Data;

/// <summary>
/// Entity Framework database context for the Nexus application.
/// </summary>
/// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
public sealed class NexusDbContext(DbContextOptions<NexusDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets the set of unified tasks.
    /// </summary>
    public DbSet<UnifiedTask> Tasks => Set<UnifiedTask>();

    /// <summary>
    /// Gets the set of projects.
    /// </summary>
    public DbSet<Project> Projects => Set<Project>();

    /// <summary>
    /// Gets the set of integrations.
    /// </summary>
    public DbSet<Integration> Integrations => Set<Integration>();

    /// <summary>
    /// Gets the set of service items.
    /// </summary>
    public DbSet<ServiceItem> ServiceItems => Set<ServiceItem>();

    /// <summary>
    /// Gets the set of reminders.
    /// </summary>
    public DbSet<Reminder> Reminders => Set<Reminder>();

    /// <summary>
    /// Gets the set of sync checkpoints.
    /// </summary>
    public DbSet<SyncCheckpoint> SyncCheckpoints => Set<SyncCheckpoint>();

    /// <summary>
    /// Configures the entity model for the context.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new UnifiedTaskConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectConfiguration());
        modelBuilder.ApplyConfiguration(new IntegrationConfiguration());
        modelBuilder.ApplyConfiguration(new ServiceItemConfiguration());
        modelBuilder.ApplyConfiguration(new ReminderConfiguration());
        modelBuilder.ApplyConfiguration(new SyncCheckpointConfiguration());
    }
}
