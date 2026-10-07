using System.Reflection;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// One row of the classification every table in the EF model carries (HO-XCT-001 §8.2/§10.3,
/// ADR-298). <paramref name="Basis"/> is a short citation (an SRS clause, an ADR, or a one-line
/// reason), never a sentence shown to a user.
/// </summary>
public sealed record TableClassification(
    string Schema,
    string Table,
    RecordClass RecordClass,
    HardDeletePolicy HardDeletePolicy,
    string Basis);

/// <summary>
/// The registry of every table's <see cref="TableClassification"/> (HO-XCT-001 §8.2/§10.3,
/// ADR-298). "Never hard-deleted" (SRS C-3, D-6) has to be true for tables that do not exist
/// yet, so <see cref="All"/> is the one place that knows every table -- HO-XCT-001's own
/// <c>RecordClassificationConventionTests</c> (not built here, ADR-298) enumerates the EF model
/// and asserts set equality against it, failing the build on any unclassified table.
///
/// <c>static partial</c> per ADR-341, one file per functional area
/// (<c>RecordClassification.[Area].cs</c>), added in the **same change** that adds the area's
/// tables (HO-XCT-001 §5.2 C-ALL-1). Each partial file contributes its own entries through a
/// <c>private static readonly IReadOnlyList&lt;TableClassification&gt;</c> field -- any name, as
/// long as it is unique in the file -- discovered by reflection in <see cref="LoadAll"/>, so a
/// new area's file needs no edit to this one and no edit to any other area's file.
///
/// A table not yet covered by any area's file, and not one of the tables §10.3 already lists
/// with the classification decided, is a documented gap, not a guess (HO-XCT-001 §8.2's closing
/// instruction: "A table that fits none of these ... is a stop. Do not guess.").
/// </summary>
public static partial class RecordClassification
{
    private static readonly Lazy<IReadOnlyList<TableClassification>> LazyAll = new(LoadAll);

    /// <summary>Every classified table, from every area's partial file.</summary>
    public static IReadOnlyList<TableClassification> All => LazyAll.Value;

    /// <summary>The classification for <paramref name="schema"/>.<paramref name="table"/>, or
    /// <c>null</c> when the table has not been classified (a gap -- see the type's remarks).</summary>
    public static TableClassification? Find(string schema, string table) =>
        All.FirstOrDefault(entry =>
            string.Equals(entry.Schema, schema, StringComparison.Ordinal) &&
            string.Equals(entry.Table, table, StringComparison.Ordinal));

    private static IReadOnlyList<TableClassification> LoadAll()
    {
        var entries = typeof(RecordClassification)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => typeof(IReadOnlyList<TableClassification>).IsAssignableFrom(field.FieldType))
            .SelectMany(field => (IReadOnlyList<TableClassification>)field.GetValue(null)!)
            .ToList();

        // ValueTuple<string, string> equality delegates to string.Equals, which is ordinal.
        var duplicates = entries
            .GroupBy(entry => (entry.Schema, entry.Table))
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.Schema}.{group.Key.Table}")
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                "RecordClassification has more than one entry for: " + string.Join(", ", duplicates) +
                ". Only the owning area's partial file may classify a given table (ADR-341).");
        }

        return entries;
    }
}
