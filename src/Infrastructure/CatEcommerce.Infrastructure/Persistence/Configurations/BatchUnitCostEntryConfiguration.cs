using CatEcommerce.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-IMP-001 §8.11 -- keyless, mapped to the view created by the <c>AddInventoryLandedCost</c> migration's raw SQL
/// (<c>InventoryLedgerSql.CreateBatchUnitCostView</c>). Never modified.</summary>
public sealed class BatchUnitCostEntryConfiguration : IEntityTypeConfiguration<BatchUnitCostEntry>
{
    public void Configure(EntityTypeBuilder<BatchUnitCostEntry> builder)
    {
        builder.HasNoKey();
        builder.ToView("v_batch_unit_cost", "inventory");

        builder.Property(x => x.BatchId).HasColumnName("batch_id");
        builder.Property(x => x.UnitCostBase).HasColumnName("unit_cost_base").HasPrecision(14, 4);
        builder.Property(x => x.CostBasis).HasColumnName("cost_basis");
    }
}
