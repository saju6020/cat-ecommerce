using System.Reflection;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// The single action-to-role table for the solution (HO-ACC-001 §12.8, ADR-036): every staff
/// action any controller guards with <c>[RequireAction]</c> maps to the staff roles
/// (<see cref="StaffRoles"/>) allowed to perform it. Unmapped actions fail closed -- there is no
/// "Administrator bypasses the table" and no wildcard.
///
/// <c>static partial</c> per ADR-341, one file per functional area (<c>ActionRoles.[Area].cs</c>).
/// Each partial file contributes its own rows through a field named ending in <c>Rows</c> (e.g.
/// <c>AccountsRows</c>) of type <c>private static readonly (string Action, string[] Roles)[]</c>
/// -- the exact pattern <c>RecordClassification.cs</c> uses for tables (HO-XCT-001 §8.2/§10.3),
/// with one addition: <see cref="BuildTable"/> finds every <c>*Rows</c>-named field **or property**
/// on the type by name first, and only then checks its shape, so a field of the wrong type, a
/// public field or a property named like a rows contribution throws immediately with a message
/// naming the offending member -- rather than being silently skipped, which would leave that
/// area's rows out of the table with no error, and every one of that area's
/// <c>[RequireAction]</c> endpoints failing closed with a 403 for a reason nobody sees. A new
/// area's own <c>ActionRoles.[Area].cs</c> file needs no edit to this one and no edit to any other
/// area's file; two areas mapping the same action is also a build-time failure (thrown from the
/// lazily-built table the first time it is read), not a silently-dropped row.
/// </summary>
public static partial class ActionRoles
{
    private const string RowsMemberSuffix = "Rows";

    private static readonly Lazy<IReadOnlyDictionary<string, string[]>> LazyTable =
        new(() => BuildTable(typeof(ActionRoles)));

    /// <summary>
    /// Merges every rows-contributing field declared anywhere on <paramref name="owner"/>'s static
    /// partial (any file), and throws if two of them map the same action. A member is identified
    /// as an intended rows contribution **by name** (ends with <see cref="RowsMemberSuffix"/>,
    /// e.g. <c>AccountsRows</c>) rather than only by type, specifically so a wrongly-shaped one
    /// -- the wrong field type, a public field, or a property instead of a field -- fails loudly
    /// here instead of being silently filtered out by a type check that never sees it as a
    /// candidate in the first place. Takes an explicit <paramref name="owner"/> (instead of
    /// hardcoding <c>typeof(ActionRoles)</c>) purely so the aggregation itself is unit-testable
    /// against throwaway types -- see <c>ActionRolesAggregationTests</c> in
    /// <c>CatEcommerce.Domain.Tests</c>, which proves a second partial's rows are picked up with
    /// no reference to the first, that a duplicate action across two partials throws, and that
    /// each wrong shape (property, public field, mismatched field type) throws with a clear
    /// message rather than being skipped.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> BuildTable(Type owner)
    {
        var rowsLikeProperty = owner
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(property => property.Name.EndsWith(RowsMemberSuffix, StringComparison.Ordinal));

        if (rowsLikeProperty is not null)
        {
            throw new InvalidOperationException(
                $"{owner.Name}.{rowsLikeProperty.Name} is named like a rows contribution (ends with " +
                $"\"{RowsMemberSuffix}\") but is a property. It must be a private static readonly " +
                "(string Action, string[] Roles)[] field, exactly like AccountsRows -- a property is " +
                "never discovered by BuildTable, so this would otherwise silently drop every row " +
                "this area maps and fail its [RequireAction] endpoints closed with no visible error.");
        }

        var rowsLikeFields = owner
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.Name.EndsWith(RowsMemberSuffix, StringComparison.Ordinal))
            .ToList();

        foreach (var field in rowsLikeFields.Where(field => field.IsPublic))
        {
            throw new InvalidOperationException(
                $"{owner.Name}.{field.Name} is named like a rows contribution but is public. It " +
                "must be private static readonly, exactly like AccountsRows.");
        }

        foreach (var field in rowsLikeFields)
        {
            if (!typeof(IReadOnlyList<(string Action, string[] Roles)>).IsAssignableFrom(field.FieldType))
            {
                throw new InvalidOperationException(
                    $"{owner.Name}.{field.Name} is named like a rows contribution but is a " +
                    $"{field.FieldType}, not a (string Action, string[] Roles)[] (or any " +
                    "IReadOnlyList<(string, string[])>), exactly like AccountsRows.");
            }
        }

        // ARCHITECTURE gate, third pass, LOW: the checks above only ever look at *Rows-named
        // members -- a correctly-shaped field that simply isn't named that way (e.g.
        // `BillingTable`) was invisible to them and silently dropped, exactly the failure mode
        // this whole method exists to prevent. Every static field of the expected shape, found by
        // TYPE regardless of name, must ALSO be named with the "Rows" suffix -- the two checks
        // (by name, by type) cross-validate each other, so neither a wrong shape nor a wrong name
        // can go unnoticed.
        var misnamedTypeMatch = owner
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => typeof(IReadOnlyList<(string Action, string[] Roles)>).IsAssignableFrom(field.FieldType))
            .FirstOrDefault(field => !field.Name.EndsWith(RowsMemberSuffix, StringComparison.Ordinal));

        if (misnamedTypeMatch is not null)
        {
            throw new InvalidOperationException(
                $"{owner.Name}.{misnamedTypeMatch.Name} has the shape of a rows contribution " +
                $"((string Action, string[] Roles)[]) but its name does not end with " +
                $"\"{RowsMemberSuffix}\", so BuildTable would not otherwise discover it. Rename it " +
                "to end with \"Rows\", exactly like AccountsRows.");
        }

        var rows = rowsLikeFields
            .SelectMany(field => (IReadOnlyList<(string Action, string[] Roles)>)field.GetValue(null)!)
            .ToList();

        var duplicates = rows
            .GroupBy(row => row.Action, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"{owner.Name} has more than one entry for: " + string.Join(", ", duplicates) +
                ". Only the owning area's own [Type].[Area].cs partial may map a given action (ADR-341).");
        }

        var table = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var row in rows)
            table[row.Action] = row.Roles;

        return table;
    }

    /// <summary>Ordinal, exact match. An action absent from the table returns <c>false</c> -- fail closed.</summary>
    public static bool TryGetRoles(string action, out IReadOnlyList<string> roles)
    {
        if (LazyTable.Value.TryGetValue(action, out var found))
        {
            roles = found;
            return true;
        }

        roles = [];
        return false;
    }
}
