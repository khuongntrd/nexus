using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Core.Entities;
using Nexus.Core.Enums;
using Nexus.Core.ValueObjects;
using TaskStatus = Nexus.Core.Enums.TaskStatus;

namespace Nexus.Infrastructure.Data.Configurations;

/// <summary>
/// Entity type configuration for the UnifiedTask entity.
/// </summary>
public sealed class UnifiedTaskConfiguration : IEntityTypeConfiguration<UnifiedTask>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UnifiedTask> builder)
    {
        builder.ToTable("unified_tasks");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.Title)
            .HasColumnName("title")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnName("description");

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(t => t.Priority)
            .HasColumnName("priority")
            .HasConversion<string>();

        builder.Property(t => t.SyncFromSource)
            .HasColumnName("sync_from_source")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(t => t.DueAt)
            .HasColumnName("due_at");

        builder.Property(t => t.ProjectId)
            .HasColumnName("project_id");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(t => t.LastSyncAt)
            .HasColumnName("last_sync_at");

        builder.OwnsOne(t => t.ExternalRef, er =>
        {
            er.Property(e => e.ServiceType)
                .HasColumnName("external_service_type")
                .HasConversion(
                    value => value.Value,
                    value => new ServiceType(value));
            er.Property(e => e.ExternalId).HasColumnName("external_id");
            er.Property(e => e.Url).HasColumnName("external_url");
            er.Property(e => e.ProjectKey).HasColumnName("external_project_key");
            er.Property(e => e.IntegrationId).HasColumnName("external_integration_id");
        });

        builder.HasIndex(t => t.Status).HasDatabaseName("ix_unified_tasks_status");
        builder.HasIndex(t => t.ProjectId).HasDatabaseName("ix_unified_tasks_project_id");
        builder.HasIndex(t => t.DueAt).HasDatabaseName("ix_unified_tasks_due_at");
    }
}
