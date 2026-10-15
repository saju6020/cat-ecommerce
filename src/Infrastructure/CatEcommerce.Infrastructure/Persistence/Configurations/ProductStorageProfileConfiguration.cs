using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Infrastructure.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>HO-IMP-001 §8.4 / §12.1. The row's id IS the product id (1:1, created on the first write); a first-write race collides on the
/// primary key and is a 409 (§12.5).</summary>
public sealed class ProductStorageProfileConfiguration : IEntityTypeConfiguration<ProductStorageProfileAggregate>
{
    public void Configure(EntityTypeBuilder<ProductStorageProfileAggregate> builder)
    {
        builder.ConfigureInventoryRoot("product_storage_profile");

        builder.Property(x => x.Condition).HasEnumeration("condition");
        builder.Property(x => x.MinTemperatureC).HasColumnName("min_temperature_c").HasPrecision(4, 1);
        builder.Property(x => x.MaxTemperatureC).HasColumnName("max_temperature_c").HasPrecision(4, 1);
        builder.Property(x => x.HandlingNotes).HasColumnName("handling_notes").HasMaxLength(InventoryRules.NoteMaxLength);
    }
}
