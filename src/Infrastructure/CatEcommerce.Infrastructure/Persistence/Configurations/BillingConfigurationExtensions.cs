using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>Shared mapping helpers for the Billing configurations (HO-BIL-001 §12.1): enumerations
/// persist by <c>Value</c>, money as <c>numeric(12,2)</c> + <c>char(3)</c>, instants as
/// <c>timestamptz</c>, and every root carries the platform's <c>xmin</c> concurrency token (ADR-016).</summary>
internal static class BillingConfigurationExtensions
{
    public static PropertyBuilder<T> HasEnumerationConversion<T>(this PropertyBuilder<T> property) where T : Enumeration =>
        property.HasConversion(v => v.Value, v => Enumeration.FromValue<T>(v));

    public static PropertyBuilder<T?> HasNullableEnumerationConversion<T>(this PropertyBuilder<T?> property) where T : Enumeration =>
        property.HasConversion(v => v == null ? 0 : v.Value, v => Enumeration.FromValue<T>(v));

    public static PropertyBuilder<decimal> HasMoneyColumn(this PropertyBuilder<decimal> property) =>
        property.HasColumnType("numeric(12,2)");

    public static PropertyBuilder<decimal?> HasMoneyColumn(this PropertyBuilder<decimal?> property) =>
        property.HasColumnType("numeric(12,2)");

    /// <summary>The platform base-class columns this solution never maps meaningfully, plus
    /// <c>xmin</c> as the row version -- identical to every other root's tail.</summary>
    public static void ConfigureRootTail<TAggregate>(this EntityTypeBuilder<TAggregate> builder)
        where TAggregate : Platform.Infrastructure.Core.Domain.AggregateRoot
    {
        builder.Property(x => x.Tags).IsRequired(false);
        builder.Property(x => x.Language).IsRequired(false);
        builder.Property(x => x.ServiceId).IsRequired(false);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Ignore(x => x.Events);
        builder.Ignore(x => x.Metadata);
    }
}
