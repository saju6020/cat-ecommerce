using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Infrastructure.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// HO-IMP-001 §8.5 / §12.1. Documents, HS codes, cost components, exchange rates and the status history are owned collections of the
/// consignment (NFR-LEG-2: nothing here is ever removed -- a document is superseded). <c>purchase_order_ids</c> is a <c>uuid[]</c> (references by
/// id, rule 2). Money is <c>numeric(12,2)</c> + <c>char(3)</c>; an exchange rate is <c>numeric(18,6)</c>.
/// </summary>
public sealed class ConsignmentConfiguration : IEntityTypeConfiguration<ConsignmentAggregate>
{
    public void Configure(EntityTypeBuilder<ConsignmentAggregate> builder)
    {
        builder.ConfigureInventoryRoot("consignment");

        builder.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.Reference).IsUnique().HasDatabaseName("ix_consignment_reference");

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.OriginCountryCode).HasColumnName("origin_country_code").HasMaxLength(2).IsFixedLength().IsRequired();
        builder.Property(x => x.ShipmentReference).HasColumnName("shipment_reference").HasMaxLength(60).IsRequired();
        builder.Property(x => x.ImportPermitNumber).HasColumnName("import_permit_number").HasMaxLength(60);
        builder.Property(x => x.HealthCertificateNumber).HasColumnName("health_certificate_number").HasMaxLength(60);
        builder.Property(x => x.CustomsDeclarationNumber).HasColumnName("customs_declaration_number").HasMaxLength(60);
        builder.Property(x => x.ExpectedArrivalOn).HasColumnName("expected_arrival_on").HasColumnType("date");
        builder.Property(x => x.Status).HasEnumeration("status");
        builder.Property(x => x.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(500);
        builder.Property(x => x.ClearedOn).HasColumnName("cleared_on").HasColumnType("date");
        builder.Property(x => x.AllocatedOn).HasColumnName("allocated_on").HasColumnType("timestamptz");
        builder.Property(x => x.AllocatedBy).HasColumnName("allocated_by");
        builder.Property(x => x.CostsChangedSinceAllocation).HasColumnName("costs_changed_since_allocation").IsRequired();

        var idList = new ValueComparer<IReadOnlyList<Guid>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)),
            v => v.ToList());
        builder.Property(x => x.PurchaseOrderIds).HasColumnName("purchase_order_ids").HasColumnType("uuid[]")
            .HasConversion(v => v.ToArray(), v => v.ToList())
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .Metadata.SetValueComparer(idList);

        builder.HasIndex(x => new { x.BranchId, x.Status }).HasDatabaseName("ix_consignment_branch_status");

        builder.OwnsMany(x => x.HsCodes, hs =>
        {
            hs.ToTable("consignment_hs_code", InventoryMapping.Schema);
            hs.WithOwner().HasForeignKey("consignment_id");
            hs.HasKey("Id");
            hs.Property(h => h.Id).ValueGeneratedNever();
            hs.Property(h => h.Code).HasColumnName("code").HasMaxLength(12).IsRequired();
            hs.Property(h => h.Description).HasColumnName("description").HasMaxLength(200);
        });
        builder.Navigation(x => x.HsCodes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.CostComponents, cost =>
        {
            cost.ToTable("consignment_cost", InventoryMapping.Schema);
            cost.WithOwner().HasForeignKey("consignment_id");
            cost.HasKey("Id");
            cost.Property(c => c.Id).ValueGeneratedNever();
            cost.Property(c => c.Kind).HasEnumeration("kind");
            cost.Property(c => c.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
            cost.Property(c => c.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsFixedLength().IsRequired();
            cost.Property(c => c.Note).HasColumnName("note").HasMaxLength(200);
        });
        builder.Navigation(x => x.CostComponents).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.ExchangeRates, rate =>
        {
            rate.ToTable("consignment_exchange_rate", InventoryMapping.Schema);
            rate.WithOwner().HasForeignKey("consignment_id");
            rate.HasKey("Id");
            rate.Property(r => r.Id).ValueGeneratedNever();
            rate.Property(r => r.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsFixedLength().IsRequired();
            rate.Property(r => r.RateToBase).HasColumnName("rate_to_base").HasPrecision(18, 6).IsRequired();
            rate.Property(r => r.RecordedOn).HasColumnName("recorded_on").HasColumnType("date").IsRequired();
            rate.HasIndex("consignment_id", nameof(ConsignmentExchangeRate.CurrencyCode)).IsUnique().HasDatabaseName("ix_consignment_exchange_rate_currency");
        });
        builder.Navigation(x => x.ExchangeRates).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Documents, doc =>
        {
            doc.ToTable("consignment_document", InventoryMapping.Schema);
            doc.WithOwner().HasForeignKey("consignment_id");
            doc.HasKey("Id");
            doc.Property(d => d.Id).ValueGeneratedNever();
            doc.Property(d => d.Kind).HasEnumeration("kind");
            doc.Property(d => d.DocumentNumber).HasColumnName("document_number").HasMaxLength(60);
            doc.Property(d => d.IssuedOn).HasColumnName("issued_on").HasColumnType("date");
            doc.Property(d => d.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
            doc.Property(d => d.StorageKey).HasColumnName("storage_key").HasMaxLength(500).IsRequired();
            doc.Property(d => d.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
            doc.Property(d => d.SizeInBytes).HasColumnName("size_in_bytes").IsRequired();
            doc.Property(d => d.UploadedBy).HasColumnName("uploaded_by").IsRequired();
            doc.Property(d => d.UploadedOn).HasColumnName("uploaded_on").HasColumnType("timestamptz").IsRequired();
            doc.Property(d => d.SupersededOn).HasColumnName("superseded_on").HasColumnType("timestamptz");
            doc.Property(d => d.SupersededBy).HasColumnName("superseded_by");
        });
        builder.Navigation(x => x.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.StatusHistory, history =>
        {
            history.ToTable("consignment_status_change", InventoryMapping.Schema);
            history.WithOwner().HasForeignKey("consignment_id");
            history.HasKey("Id");
            history.Property(h => h.Id).ValueGeneratedNever();
            history.Property(h => h.From).HasEnumeration("from_status");
            history.Property(h => h.To).HasEnumeration("to_status");
            history.Property(h => h.ChangedOn).HasColumnName("changed_on").HasColumnType("timestamptz").IsRequired();
            history.Property(h => h.ChangedBy).HasColumnName("changed_by").IsRequired();
            history.Property(h => h.EffectiveOn).HasColumnName("effective_on").HasColumnType("date");
            history.Property(h => h.Sequence).HasColumnName("sequence").IsRequired();
        });
        builder.Navigation(x => x.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.LandedCost, line =>
        {
            line.ToTable("consignment_landed_cost_line", InventoryMapping.Schema);
            line.WithOwner().HasForeignKey("consignment_id");
            line.HasKey("Id");
            line.Property(l => l.Id).ValueGeneratedNever();
            line.Property(l => l.BatchId).HasColumnName("batch_id").IsRequired();
            line.Property(l => l.Quantity).HasColumnName("quantity").HasPrecision(12, 3).IsRequired();
            line.Property(l => l.PurchaseCostBase).HasColumnName("purchase_cost_base").HasPrecision(14, 2).IsRequired();
            line.Property(l => l.AllocatedCostBase).HasColumnName("allocated_cost_base").HasPrecision(14, 2).IsRequired();
            line.Property(l => l.LandedUnitCostBase).HasColumnName("landed_unit_cost_base").HasPrecision(14, 4).IsRequired();
            line.HasIndex(l => l.BatchId).HasDatabaseName("ix_consignment_landed_cost_line_batch");
        });
        builder.Navigation(x => x.LandedCost).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
