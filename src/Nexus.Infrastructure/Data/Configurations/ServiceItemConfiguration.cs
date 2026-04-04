using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Core.Entities;
using Nexus.Core.ValueObjects;

namespace Nexus.Infrastructure.Data.Configurations;

/// <summary>
/// Entity type configuration for the ServiceItem entity.
/// </summary>
public sealed class ServiceItemConfiguration : IEntityTypeConfiguration<ServiceItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ServiceItem> builder)
    {
        builder.ToTable("service_items");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.ServiceType)
            .HasColumnName("service_type")
            .HasConversion(
                value => value.Value,
                value => new ServiceType(value))
            .IsRequired();

        builder.Property(s => s.ExternalId)
            .HasColumnName("external_id")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(s => s.RawJson)
            .HasColumnName("raw_json")
            .IsRequired();

        builder.Property(s => s.FetchedAt)
            .HasColumnName("fetched_at")
            .IsRequired();

        builder.HasIndex(s => new { s.ServiceType, s.ExternalId })
            .IsUnique()
            .HasDatabaseName("ix_service_items_service_type_external_id");
    }
}
