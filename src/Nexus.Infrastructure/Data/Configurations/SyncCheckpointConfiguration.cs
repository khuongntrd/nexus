using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Core.Entities;
using Nexus.Core.ValueObjects;

namespace Nexus.Infrastructure.Data.Configurations;

/// <summary>
/// Entity type configuration for the SyncCheckpoint entity.
/// </summary>
public sealed class SyncCheckpointConfiguration : IEntityTypeConfiguration<SyncCheckpoint>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SyncCheckpoint> builder)
    {
        builder.ToTable("sync_checkpoints");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.ServiceType)
            .HasColumnName("service_type")
            .HasConversion(
                value => value.Value,
                value => new ServiceType(value))
            .IsRequired();

        builder.Property(s => s.Cursor)
            .HasColumnName("cursor");

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(s => s.ServiceType)
            .IsUnique()
            .HasDatabaseName("ix_sync_checkpoints_service_type");
    }
}
