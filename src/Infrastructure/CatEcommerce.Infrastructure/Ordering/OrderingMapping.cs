using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>
/// HO-ORD-001 §12.1 -- the conventions every Ordering configuration applies, in one place so none of them can forget one: schema
/// <c>ordering</c>, enumerations persisted by <c>Value</c> (ADR-022), money as <c>numeric(12,2)</c>, instants as <c>timestamptz</c>, and
/// ADR-021's three corrections on every root (<c>ValueGeneratedNever()</c> on the id; <c>Tags</c>/<c>Language</c>/<c>ServiceId</c> optional; the
/// <c>xmin</c> row version, with <c>Events</c> and <c>Metadata</c> ignored).
/// </summary>
internal static class OrderingMapping
{
    public const string Schema = "ordering";

    /// <summary>A root table in the <c>ordering</c> schema with ADR-021's corrections applied.</summary>
    public static EntityTypeBuilder<T> ConfigureOrderingRoot<T>(this EntityTypeBuilder<T> builder, string table) where T : AggregateRoot
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

    /// <summary><c>numeric(12,2)</c> money (ADR-054/ADR-317).</summary>
    public static PropertyBuilder<decimal> HasMoney(this PropertyBuilder<decimal> property, string column) =>
        property.HasColumnName(column).HasColumnType("numeric(12,2)");

    public static PropertyBuilder<decimal?> HasMoney(this PropertyBuilder<decimal?> property, string column) =>
        property.HasColumnName(column).HasColumnType("numeric(12,2)");

    public static PropertyBuilder<DateTimeOffset> HasInstant(this PropertyBuilder<DateTimeOffset> property, string column) =>
        property.HasColumnName(column).HasColumnType("timestamptz");

    public static PropertyBuilder<DateTimeOffset?> HasInstant(this PropertyBuilder<DateTimeOffset?> property, string column) =>
        property.HasColumnName(column).HasColumnType("timestamptz");
}
