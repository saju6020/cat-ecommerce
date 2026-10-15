using System.Text.RegularExpressions;

namespace CatEcommerce.Infrastructure.Tests.Retention;

/// <summary>
/// HO-XCT-001 U-A2 / ADR-299 layer 3. <c>ExecuteDelete</c> and raw SQL bypass the EF interceptor (F34), so the build scans
/// <c>src/**/*.cs</c> (excluding <c>Migrations/</c>, <c>obj/</c>, <c>bin/</c>) for the bypasses. Only <c>RetentionPurger</c> may
/// delete rows by <c>ExecuteDelete</c> or raw SQL; trigger-disabling statements are forbidden everywhere, migrations included.
/// </summary>
public sealed class HardDeleteSourceScanTests
{
    /// <summary>
    /// Named exceptions to "only RetentionPurger.cs", each with its reason. Deviation from section 12.1: Ordering's idempotency store was
    /// shipped before this scan existed and deletes from <c>ops.idempotency_record</c> (a Purgeable working set, 24-hour life,
    /// HO-ORD-001 ADR-202). It is listed, not rewritten, because the handover assigns no change to Ordering's files; it is in
    /// docs/backlog/XCT.md for the integrator to fold into <c>RetentionPurger</c>.
    /// </summary>
    private static readonly Dictionary<string, string> AllowedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Infrastructure/CatEcommerce.Infrastructure/Retention/RetentionPurger.cs"] = "The one sanctioned purger (ADR-299, ADR-304).",
        ["Infrastructure/CatEcommerce.Infrastructure/Ordering/EfIdempotencyStore.cs"] = "HO-ORD-001 ADR-202: releases and purges its own Purgeable ops.idempotency_record rows; shipped before this scan.",
    };

    private static readonly Regex ExecuteDelete = new(@"\bExecuteDelete(Async)?\s*\(", RegexOptions.Compiled);
    private static readonly Regex RawSqlCall = new(@"\b(ExecuteSqlRaw|ExecuteSqlRawAsync|ExecuteSqlInterpolated|ExecuteSqlInterpolatedAsync|SqlQueryRaw|SqlQuery)\s*(<[^>]*>)?\s*\(", RegexOptions.Compiled);
    private static readonly Regex DeleteOrTruncate = new(@"\b(DELETE\s+FROM|TRUNCATE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TriggerSwitchOff = new(@"DISABLE\s+TRIGGER|session_replication_role", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static string SrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CatEcommerce.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src");
    }

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(SrcRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static string Relative(string path) => Path.GetRelativePath(SrcRoot(), path).Replace('\\', '/');

    /// <summary>The text of each raw-SQL call: from the call to the first ';' (a statement ends there).</summary>
    private static IEnumerable<string> RawSqlArguments(string text)
    {
        foreach (Match match in RawSqlCall.Matches(text))
        {
            var end = text.IndexOf(';', match.Index);
            yield return end < 0 ? text[match.Index..] : text[match.Index..end];
        }
    }

    // U-A2
    [Fact]
    public void U_A2_NoBypassOfTheHardDeleteGuard_ExistsOutsideThePurger()
    {
        var offenders = new List<string>();

        foreach (var path in SourceFiles())
        {
            var relative = Relative(path);
            if (relative.Contains("/Migrations/", StringComparison.Ordinal)) continue;
            if (AllowedFiles.ContainsKey(relative)) continue;

            var text = File.ReadAllText(path);
            if (ExecuteDelete.IsMatch(text)) offenders.Add($"{relative}: ExecuteDelete");
            if (RawSqlArguments(text).Any(arguments => DeleteOrTruncate.IsMatch(arguments))) offenders.Add($"{relative}: raw DELETE/TRUNCATE");
        }

        Assert.True(offenders.Count == 0, "Hard-delete bypasses outside RetentionPurger.cs: " + string.Join("; ", offenders));
    }

    [Fact]
    public void U_A2_NothingSwitchesATriggerOffOrChangesTheReplicationRole_MigrationsIncluded()
    {
        var offenders = SourceFiles()
            .Where(path => TriggerSwitchOff.IsMatch(File.ReadAllText(path)))
            .Select(Relative)
            .ToList();

        Assert.True(offenders.Count == 0, "Trigger-disabling statements found: " + string.Join("; ", offenders));
    }

    [Fact]
    public void U_A2_EveryNamedException_StillExists_SoTheListCannotRot()
    {
        foreach (var relative in AllowedFiles.Keys.Where(r => !r.EndsWith("RetentionPurger.cs", StringComparison.Ordinal)))
            Assert.True(File.Exists(Path.Combine(SrcRoot(), relative)), $"{relative} is listed as an exception but no longer exists; remove it from the list.");
    }
}
