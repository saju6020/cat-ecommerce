using System.Reflection;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-ADM-001 §8.7 / ADR-050. One registry entry per business setting. Bounds/Default are stored
/// as invariant-culture strings -- exactly how <c>SettingValue</c> stores a value -- so a
/// <c>Decimal</c> setting's default (e.g. "30.00") round-trips without a culture-dependent parse.
/// <c>Min</c>/<c>Max</c> are <c>null</c> for <see cref="SettingType.Boolean"/>,
/// <see cref="SettingType.TimeRange"/> ("any" bounds, HO-ADM-001 §8.7) and
/// <see cref="SettingType.CurrencyCode"/>.
/// </summary>
public sealed record SettingDefinition(
    string Key,
    string Group,
    SettingType Type,
    string? Min,
    string? Max,
    string? Unit,
    string Default,
    bool BranchOverridable,
    string DisplayName,
    string Description);

/// <summary>
/// The business settings registry -- HO-ADM-001 §10.2. <c>static partial</c> per ADR-341: each
/// area that needs a setting not yet registered appends its own entries in its own
/// <c>SettingDefinitions.[Area].cs</c> file, discovered here by reflection exactly like
/// <c>RecordClassification</c>/<c>ActionRoles</c> -- never editing this file or another area's.
/// </summary>
public static partial class SettingDefinitions
{
    private static readonly Lazy<IReadOnlyList<SettingDefinition>> LazyAll = new(() => BuildRegistry(typeof(SettingDefinitions)));

    public static IReadOnlyList<SettingDefinition> All => LazyAll.Value;

    public static SettingDefinition? Find(string key) =>
        All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// Merges every settings-contributing field declared anywhere on <paramref name="owner"/>'s
    /// static partial. Takes an explicit <paramref name="owner"/> (instead of hardcoding
    /// <c>typeof(SettingDefinitions)</c>) so the aggregation itself is unit-testable against
    /// throwaway types, exactly like <see cref="ActionRoles.BuildTable"/> -- see
    /// <c>SettingDefinitionsAggregationTests</c>.
    ///
    /// Gate fix 2026-09-26: previously only <c>NonPublic|Static</c> fields were ever inspected, so
    /// a public field or a property of the right shape was invisible to this method -- silently
    /// dropping every setting it defines, with no error, exactly the failure mode
    /// <see cref="ActionRoles.BuildTable"/> already guards against for its own rows.
    /// </summary>
    public static IReadOnlyList<SettingDefinition> BuildRegistry(Type owner)
    {
        var publicFieldMatch = owner
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(field => typeof(IReadOnlyList<SettingDefinition>).IsAssignableFrom(field.FieldType));

        if (publicFieldMatch is not null)
        {
            throw new InvalidOperationException(
                $"{owner.Name}.{publicFieldMatch.Name} has the shape of a settings contribution " +
                "(IReadOnlyList<SettingDefinition>) but is public. It must be private static readonly, " +
                "exactly like AdministrationEntries -- a public field of this shape is invisible to " +
                "this method's reflection, so this would otherwise silently drop every setting it " +
                "defines with no error.");
        }

        var propertyMatch = owner
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(property =>
                property.Name != nameof(All) &&
                typeof(IReadOnlyList<SettingDefinition>).IsAssignableFrom(property.PropertyType));

        if (propertyMatch is not null)
        {
            throw new InvalidOperationException(
                $"{owner.Name}.{propertyMatch.Name} has the shape of a settings contribution but is " +
                "a property. It must be a private static readonly IReadOnlyList<SettingDefinition> " +
                "field, exactly like AdministrationEntries -- a property is never discovered by this method.");
        }

        var entries = owner
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => typeof(IReadOnlyList<SettingDefinition>).IsAssignableFrom(field.FieldType))
            .SelectMany(field => (IReadOnlyList<SettingDefinition>)field.GetValue(null)!)
            .ToList();

        var duplicates = entries
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"{owner.Name} has more than one entry for: " + string.Join(", ", duplicates) +
                ". Only the owning area's partial file may define a given key (ADR-341).");
        }

        return entries;
    }
}
