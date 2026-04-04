using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Core.Entities;
using Nexus.Core.ValueObjects;

namespace Nexus.Infrastructure.Data.Configurations;

/// <summary>
/// Entity type configuration for the Project entity.
/// </summary>
public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(500)
            .IsRequired();

        builder.OwnsOne(p => p.ExternalRef, er =>
        {
            er.Property(e => e.ServiceType)
                .HasColumnName("external_service_type")
                .HasConversion(
                    value => value.Value,
                    value => new ServiceType(value));
            er.Property(e => e.ExternalId).HasColumnName("external_id");
            er.Property(e => e.Url).HasColumnName("external_url");
            er.Property(e => e.ProjectKey).HasColumnName("external_project_key");
        });
    }
}
