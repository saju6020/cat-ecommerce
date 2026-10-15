using System.Reflection;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Persistence.ReportingViews;

/// <summary>
/// HO-RPT-001 section 12.1 / ADR-278 -- loads a versioned view definition (<c>rv_x.v1.sql</c>) embedded in this assembly. A Reporting migration passes it to <c>migrationBuilder.Sql</c>; an applied
/// migration's SQL never changes, so changing a view is a NEW file version and a NEW migration.
/// </summary>
public static class ReportingViewSql
{
    private const string ResourcePrefix = "CatEcommerce.Infrastructure.Persistence.ReportingViews.Sql.";

    /// <summary>
    /// Loads <paramref name="versionedName"/> and replaces every <c>{{TOKEN}}</c> with its value. Enumeration values used by a view (the hold statuses a schedule excludes, the sent notification
    /// statuses) are passed from the enumerations in the migration code, never typed as literals in the SQL (U-X3).
    /// </summary>
    public static string Load(string versionedName, params (string Token, string Value)[] tokens)
    {
        var sql = LoadRaw(versionedName);
        foreach (var (token, value) in tokens) sql = sql.Replace("{{" + token + "}}", value, StringComparison.Ordinal);

        if (sql.Contains("{{", StringComparison.Ordinal))
            throw new InvalidOperationException($"The view definition '{versionedName}.sql' has a token with no value.");

        return sql;
    }

    /// <summary>The values of enumeration members as a comma-separated SQL list.</summary>
    public static string List(params Enumeration[] members) => string.Join(",", members.Select(m => m.Value));

    public static string Drop(string viewName) => $"DROP VIEW IF EXISTS report.{viewName};";

    public static IReadOnlyList<string> EmbeddedNames() =>
        typeof(ReportingViewSql).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(n => n[ResourcePrefix.Length..^".sql".Length])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    private static string LoadRaw(string versionedName)
    {
        var assembly = typeof(ReportingViewSql).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + versionedName + ".sql")
            ?? throw new InvalidOperationException($"The embedded view definition '{versionedName}.sql' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
