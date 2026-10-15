using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Infrastructure.Inventory;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-IMP-001 §8.3 / §12.1. <c>ix_stock_location_branch_code</c> is the code-unique-per-branch rule;
/// <c>ix_stock_location_one_quarantine</c> is "exactly one ACTIVE quarantine location per branch" (the filter is built from the
/// enumeration, never a literal -- U-X2).</summary>
public sealed class StockLocationConfiguration : IEntityTypeConfiguration<StockLocationAggregate>
{
    public void Configure(EntityTypeBuilder<StockLocationAggregate> builder)
    {
        builder.ConfigureInventoryRoot("stock_location");

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(InventoryRules.MaxLocationCodeLength).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Kind).HasEnumeration("kind");
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(x => new { x.BranchId, x.Code }).IsUnique().HasDatabaseName("ix_stock_location_branch_code");
        builder.HasIndex(x => x.BranchId).IsUnique().HasDatabaseName("ix_stock_location_one_quarantine")
            .HasFilter($"kind = {StockLocationKind.Quarantine.Value} AND is_active");
    }
}
