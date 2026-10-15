using System.Reflection;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>One typed column of a report (HO-RPT-001 section 8.2). <paramref name="RestrictedToAction"/> removes the column from the payload, the CSV and the PDF for a caller who does not hold it.</summary>
public sealed record ReportColumn(
    string Key, string Label, ReportColumnType Type, string? Unit = null, string? RestrictedToAction = null, bool Sortable = true, string? Description = null);

/// <summary>A report parameter beyond the date range and the branch. Validated server-side; an unknown parameter is refused.</summary>
public sealed record ReportParameter(
    string Name, string Label, ReportParameterType Type, string Default, IReadOnlyList<string>? AllowedValues = null, int? Min = null, int? Max = null);

/// <summary>
/// One report of the catalogue (HO-RPT-001 section 8.2, ADR-280). Every report takes <c>from</c>, <c>to</c> and a branch (FR-RPT-1) and offers both CSV and PDF (US-RPT-01 AC2),
/// so neither is a field here. <paramref name="SourceAreas"/> documents which handovers' tables the report's views read.
/// </summary>
public sealed record ReportDefinition(
    string Key,
    string Title,
    string Description,
    ReportCategory Category,
    ReportKind Kind,
    ReportRelativePeriod DefaultPeriod,
    int MaxRangeDays,
    IReadOnlyList<ReportParameter> Parameters,
    IReadOnlyList<ReportColumn> Columns,
    string? DefaultSort,
    bool HasTotals,
    bool TimeBucketed,
    IReadOnlyList<string> SourceAreas)
{
    public ReportColumn? FindColumn(string key) => Columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal));

    public ReportParameter? FindParameter(string name) => Parameters.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// The one list of reports (HO-RPT-001 section 8.2, ADR-280). <c>static partial</c> so each phase contributes its definitions in its own file through a private static field whose
/// name ends in <c>Definitions</c>, discovered by reflection like <c>ActionRoles</c>; a duplicate key throws the first time the catalogue is read.
/// </summary>
public static partial class ReportCatalogue
{
    private const string DefinitionsSuffix = "Definitions";

    private static readonly Lazy<IReadOnlyList<ReportDefinition>> LazyAll = new(() => Build(typeof(ReportCatalogue)));

    public static IReadOnlyList<ReportDefinition> All => LazyAll.Value;

    public static ReportDefinition? Find(string key) => All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.Ordinal));

    public static IReadOnlyList<ReportDefinition> Build(Type owner)
    {
        var fields = owner.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.Name.EndsWith(DefinitionsSuffix, StringComparison.Ordinal))
            .ToList();

        foreach (var field in fields)
        {
            if (!field.IsPrivate || !typeof(IReadOnlyList<ReportDefinition>).IsAssignableFrom(field.FieldType))
            {
                throw new InvalidOperationException(
                    $"{owner.Name}.{field.Name} is named like a report contribution but is not a private static IReadOnlyList<ReportDefinition> field.");
            }
        }

        var all = fields.SelectMany(f => (IReadOnlyList<ReportDefinition>)f.GetValue(null)!).ToList();

        var duplicate = all.GroupBy(d => d.Key, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"{owner.Name} defines the report key '{duplicate.Key}' more than once.");

        return all.OrderBy(d => d.Category.Value).ThenBy(d => d.Key, StringComparer.Ordinal).ToList();
    }
}
