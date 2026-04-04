using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Core.Entities;
using Nexus.Core.ValueObjects;

namespace Nexus.Infrastructure.Data.Configurations;

/// <summary>
/// Entity type configuration for the Integration entity.
/// </summary>
public sealed class IntegrationConfiguration : IEntityTypeConfiguration<Integration>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Integration> builder)
    {
        builder.ToTable("integrations");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");

        builder.Property(i => i.ServiceType)
            .HasColumnName("service_type")
            .HasConversion(
                value => value.Value,
                value => new ServiceType(value))
            .IsRequired();

        builder.Property(i => i.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.AuthMode)
            .HasColumnName("auth_mode")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(i => i.LastSyncAt)
            .HasColumnName("last_sync_at");

        builder.Property(i => i.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired();

        builder.Property(i => i.ConfigJson)
            .HasColumnName("config_json");

        builder.Property(i => i.TokenJson)
            .HasColumnName("token_json");

        builder.HasIndex(i => i.ServiceType)
            .HasDatabaseName("ix_integrations_service_type");
    }
}
