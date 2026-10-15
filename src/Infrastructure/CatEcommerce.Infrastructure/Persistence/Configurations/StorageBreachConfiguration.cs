using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Infrastructure.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-IMP-001 section 8.13 / 12.1 (US-IMP-16 AC2, ADR-115) -- a storage-condition breach report; the affected batches are a snapshot taken when it was reported.</summary>
public sealed class StorageBreachReportConfiguration : IEntityTypeConfiguration<StorageBreachReportAggregate>
{
    public void Configure(EntityTypeBuilder<StorageBreachReportAggregate> builder)
    {
        builder.ConfigureInventoryRoot("storage_breach_report");

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.LocationId).HasColumnName("location_id").IsRequired();
        builder.Property(x => x.ObservedOn).HasColumnName("observed_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.Condition).HasColumnName("condition").HasMaxLength(500).IsRequired();
        builder.Property(x => x.TemperatureC).HasColumnName("temperature_c").HasPrecision(5, 1);
        builder.Property(x => x.DurationMinutes).HasColumnName("duration_minutes");
        builder.Property(x => x.Status).HasEnumeration("status");
        builder.Property(x => x.ReportedBy).HasColumnName("reported_by").IsRequired();
        builder.Property(x => x.ReportedOn).HasColumnName("reported_on").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.QuarantinedOn).HasColumnName("quarantined_on").HasColumnType("timestamptz");
        builder.Property(x => x.QuarantinedBy).HasColumnName("quarantined_by");
        builder.Property(x => x.ClosedOn).HasColumnName("closed_on").HasColumnType("timestamptz");
        builder.Property(x => x.ClosedBy).HasColumnName("closed_by");
        builder.Property(x => x.ClosingNote).HasColumnName("closing_note").HasMaxLength(InventoryRules.NoteMaxLength);

        var idList = new ValueComparer<IReadOnlyList<Guid>>(
            (a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)), v => v.ToList());
        builder.Property(x => x.AffectedBatchIds).HasColumnName("affected_batch_ids").HasColumnType("uuid[]")
            .HasConversion(v => v.ToArray(), v => v.ToList())
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .Metadata.SetValueComparer(idList);

        builder.HasIndex(x => new { x.BranchId, x.Status }).HasDatabaseName("ix_storage_breach_report_branch_status");
        builder.HasIndex(x => x.LocationId).HasDatabaseName("ix_storage_breach_report_location");
    }
}
