using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>
/// HO-ORD-001 §12.1 -- small value lists kept as <c>jsonb</c> (postal codes, weight bands, slot windows, scope ids, credit-note ids). Written and
/// read explicitly with <c>System.Text.Json</c> and a value comparer, because a <c>List&lt;T&gt;</c> otherwise maps as a primitive collection.
/// </summary>
internal static class OrderingJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);

    public static PropertyBuilder<IReadOnlyList<T>> HasJsonbList<T>(this PropertyBuilder<IReadOnlyList<T>> property, string column) =>
        property.HasColumnName(column).HasColumnType("jsonb").IsRequired()
            .HasConversion(
                list => JsonSerializer.Serialize(list, Options),
                json => (IReadOnlyList<T>)(JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>()),
                new ValueComparer<IReadOnlyList<T>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                    list => list.ToList()));
}
