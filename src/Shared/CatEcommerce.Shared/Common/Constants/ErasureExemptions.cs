using System.Reflection;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-XCT-001 §5.2 C-ALL-2 / §8.8. A context that stores <b>no</b> customer personal data beyond
/// the id is exempted from registering an <c>IPersonalDataEraser</c> here, with a one-line reason
/// -- otherwise HO-XCT-001's own <c>ErasureCoverageConventionTests</c> (U-B6, not built here) fails
/// for that context.
///
/// <c>static partial</c> per ADR-341, one file per functional area
/// (<c>ErasureExemptions.[Area].cs</c>), following the same discover-by-reflection pattern as
/// <see cref="RecordClassification"/>: each partial file contributes its own entries through a
/// <c>private static readonly IReadOnlyList&lt;(string ContextKey, string Reason)&gt;</c> field,
/// discovered in <see cref="LoadAll"/>, so a new area's file needs no edit to this one and no edit
/// to any other area's file.
///
/// This base file is not yet built by HO-XCT-001 itself (its own §13 names it) -- created early
/// here because HO-APT-001's own gate fix (ARCHITECTURE HIGH #4, 2026-09-28) needs a registration
/// point for its own exemption entry (§8.8/OQ-7 exempts Appointments from erasure). HO-XCT-001 may
/// reshape this when it lands, per its own design.
/// </summary>
public static partial class ErasureExemptions
{
    private static readonly Lazy<IReadOnlyList<(string ContextKey, string Reason)>> LazyAll = new(LoadAll);

    /// <summary>Every exempted context, from every area's partial file.</summary>
    public static IReadOnlyList<(string ContextKey, string Reason)> All => LazyAll.Value;

    public static bool IsExempt(string contextKey) =>
        All.Any(entry => string.Equals(entry.ContextKey, contextKey, StringComparison.Ordinal));

    private static IReadOnlyList<(string ContextKey, string Reason)> LoadAll()
    {
        var entries = typeof(ErasureExemptions)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => typeof(IReadOnlyList<(string ContextKey, string Reason)>).IsAssignableFrom(field.FieldType))
            .SelectMany(field => (IReadOnlyList<(string ContextKey, string Reason)>)field.GetValue(null)!)
            .ToList();

        var duplicates = entries
            .GroupBy(entry => entry.ContextKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                "ErasureExemptions has more than one entry for: " + string.Join(", ", duplicates) +
                ". Only the owning area's partial file may exempt a given context (ADR-341).");
        }

        return entries;
    }
}
