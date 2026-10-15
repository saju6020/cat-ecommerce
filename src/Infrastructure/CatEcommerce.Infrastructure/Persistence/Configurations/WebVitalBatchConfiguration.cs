using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-XCT-001 section 8.6 / 12.1 -- <c>ops.web_vital_batch</c> and its owned <c>ops.web_vital_sample</c>. Telemetry, Purgeable (ADR-309).</summary>
public sealed class WebVitalBatchConfiguration : IEntityTypeConfiguration<WebVitalBatchAggregate>
{
    public void Configure(EntityTypeBuilder<WebVitalBatchAggregate> builder)
    {
        builder.ToTable("web_vital_batch", "ops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.ReceivedOn).HasColumnName("received_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.BuildId).HasColumnName("build_id").HasMaxLength(WebVitalBatchAggregate.MaxBuildIdLength).IsRequired();

        builder.OwnsMany(x => x.Samples, sample =>
        {
            sample.ToTable("web_vital_sample", "ops");
            sample.WithOwner().HasForeignKey("web_vital_batch_id");
            sample.HasKey(s => s.Id);
            sample.Property(s => s.Id).ValueGeneratedNever();
            sample.Property(s => s.Metric).HasColumnName("metric")
                .HasConversion(v => v.Value, v => Enumeration.FromValue<WebVitalMetric>(v)).IsRequired();
            sample.Property(s => s.Value).HasColumnName("value").HasColumnType("numeric(12,4)").IsRequired();
            sample.Property(s => s.RouteTemplate).HasColumnName("route_template").HasMaxLength(WebVitalBatchAggregate.MaxRouteLength).IsRequired();
            sample.Property(s => s.NavigationType).HasColumnName("navigation_type").HasMaxLength(20).IsRequired();
            sample.Property(s => s.EffectiveConnectionType).HasColumnName("effective_connection_type").HasMaxLength(10);
            sample.Property(s => s.ViewportClass).HasColumnName("viewport_class")
                .HasConversion(v => v.Value, v => Enumeration.FromValue<ViewportClass>(v)).IsRequired();

            sample.HasIndex(s => new { s.RouteTemplate, s.Metric, s.ViewportClass }).HasDatabaseName("ix_web_vital_sample_route_metric_viewport");
        });
        builder.Navigation(x => x.Samples).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.ReceivedOn).HasDatabaseName("ix_web_vital_batch_received_on");

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
