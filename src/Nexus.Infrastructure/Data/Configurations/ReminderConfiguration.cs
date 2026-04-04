using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Core.Entities;

namespace Nexus.Infrastructure.Data.Configurations;

/// <summary>
/// Entity type configuration for the Reminder entity.
/// </summary>
public sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("reminders");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.TaskId)
            .HasColumnName("task_id")
            .IsRequired();

        builder.Property(r => r.FireAt)
            .HasColumnName("fire_at")
            .IsRequired();

        builder.Property(r => r.IsFired)
            .HasColumnName("is_fired")
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(r => r.FireAt).HasDatabaseName("ix_reminders_fire_at");
        builder.HasIndex(r => r.TaskId).HasDatabaseName("ix_reminders_task_id");
        builder.HasIndex(r => r.IsFired).HasDatabaseName("ix_reminders_is_fired");
    }
}
