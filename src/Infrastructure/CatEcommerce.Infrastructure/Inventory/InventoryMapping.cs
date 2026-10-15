using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>
/// HO-IMP-001 §12.1 -- the conventions every Inventory configuration applies, in one place so none of them can forget one: schema
/// <c>inventory</c>, enumerations persisted by <c>Value</c> (ADR-022), and ADR-021's three corrections on every root
/// (<c>ValueGeneratedNever()</c> on the id; <c>Tags</c>/<c>Language</c>/<c>ServiceId</c> optional; the <c>xmin</c> row version, with
/// <c>Events</c> and <c>Metadata</c> ignored).
/// </summary>
internal static class InventoryMapping
{
    public const string Schema = "inventory";

    /// <summary>A root table in the <c>inventory</c> schema with ADR-021's corrections applied.</summary>
    public static EntityTypeBuilder<T> ConfigureInventoryRoot<T>(this EntityTypeBuilder<T> builder, string table) where T : AggregateRoot
    {
        builder.ToTable(table, Schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);

        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
        return builder;
    }

    /// <summary>Persists a required enumeration by its <c>Value</c>.</summary>
    public static PropertyBuilder<T> HasEnumeration<T>(this PropertyBuilder<T> property, string column) where T : Enumeration =>
        property.HasColumnName(column).HasConversion(v => v.Value, v => Enumeration.FromValue<T>(v)).IsRequired();

    /// <summary>Persists an optional enumeration by its <c>Value</c> (null stays null).</summary>
    public static PropertyBuilder<T?> HasNullableEnumeration<T>(this PropertyBuilder<T?> property, string column) where T : Enumeration =>
        property.HasColumnName(column)
            .HasConversion(v => v == null ? (int?)null : v.Value, v => v == null ? null : Enumeration.FromValue<T>(v.Value));
}
