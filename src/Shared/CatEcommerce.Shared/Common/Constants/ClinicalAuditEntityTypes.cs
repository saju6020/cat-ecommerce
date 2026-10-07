using System.Reflection;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// The set of audit <c>EntityType</c> strings that carry clinical content (owner decision (a),
/// HO-ADM-001, gate fix 2026-09-26). <c>GetAuditEntriesQueryHandler</c> (Read layer -- this,
/// Shared, project never references upward into Read, even in a doc comment) redacts
/// <c>BeforeValue</c>/<c>AfterValue</c>/<c>Reason</c> for
/// any row whose <c>EntityType</c> is in this set, unless the caller holds
/// <see cref="Actions.Cats.View"/>.
///
/// <c>static partial</c> per ADR-341, same pattern as <c>SettingDefinitions</c>/<c>ActionRoles</c>/
/// <c>RecordClassification</c>: a clinical area (HO-CAT-001 today; HO-EMR-001, HO-PHM-001,
/// HO-SRG-001 later) registers the audit <c>EntityType</c> strings its own event handlers actually
/// write in its own <c>ClinicalAuditEntityTypes.[Area].cs</c> file, contributing a private static
/// readonly <c>IReadOnlyList&lt;string&gt;</c> field of any name -- discovered by reflection in
/// <see cref="BuildRegistry"/>, closing the "closed, hard-coded list that fails open for every
/// later clinical area" gap: without this registry, redaction would need Administration's own file
/// edited by every clinical area that lands later, which ADR-341's per-area partials exist to
/// prevent.
/// </summary>
public static partial class ClinicalAuditEntityTypes
{
    private static readonly Lazy<IReadOnlyList<string>> LazyAll = new(() => BuildRegistry(typeof(ClinicalAuditEntityTypes)));

    public static IReadOnlyList<string> All => LazyAll.Value;

    public static bool Contains(string entityType) => All.Contains(entityType, StringComparer.Ordinal);

    /// <summary>Takes an explicit <paramref name="owner"/> (instead of hardcoding
    /// <c>typeof(ClinicalAuditEntityTypes)</c>) so the aggregation itself is unit-testable against
    /// throwaway types, exactly like <see cref="ActionRoles.BuildTable"/> and
    /// <see cref="SettingDefinitions.BuildRegistry"/> -- including their discipline of throwing on
    /// a public field or a property of the contribution shape, rather than silently skipping it.</summary>
    public static IReadOnlyList<string> BuildRegistry(Type owner)
    {
        var publicFieldMatch = owner
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(field => typeof(IReadOnlyList<string>).IsAssignableFrom(field.FieldType));

        if (publicFieldMatch is not null)
        {
            throw new InvalidOperationException(
                $"{owner.Name}.{publicFieldMatch.Name} has the shape of a clinical-entity-type " +
                "contribution (IReadOnlyList<string>) but is public. It must be private static " +
                "readonly -- a public field of this shape is invisible to this method's reflection, " +
                "so this would otherwise silently drop every entity type it registers with no error.");
        }

        var propertyMatch = owner
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(property =>
                property.Name != nameof(All) &&
                typeof(IReadOnlyList<string>).IsAssignableFrom(property.PropertyType));

        if (propertyMatch is not null)
        {
            throw new InvalidOperationException(
                $"{owner.Name}.{propertyMatch.Name} has the shape of a clinical-entity-type " +
                "contribution but is a property. It must be a private static readonly " +
                "IReadOnlyList<string> field -- a property is never discovered by this method.");
        }

        var entries = owner
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => typeof(IReadOnlyList<string>).IsAssignableFrom(field.FieldType))
            .SelectMany(field => (IReadOnlyList<string>)field.GetValue(null)!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return entries;
    }
}
