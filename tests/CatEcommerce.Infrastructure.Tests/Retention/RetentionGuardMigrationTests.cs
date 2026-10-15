using System.Text.RegularExpressions;
using CatEcommerce.Infrastructure.Retention;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Tests.Retention;

/// <summary>HO-XCT-001 U-A7 -- the inlined trigger list in <c>AddRetentionGuards</c> against <see cref="RecordClassification"/>.</summary>
public sealed class RetentionGuardMigrationTests
{
    private static bool NeedsOwnTrigger(TableClassification e) =>
        e.HardDeletePolicy == HardDeletePolicy.Forbidden
        && !RetentionGuardSql.DedicatedlyGuardedTables.Any(d => d.Schema == e.Schema && d.Table == e.Table);

    private static IEnumerable<string> ForbiddenTables() =>
        RecordClassification.All
            .Where(NeedsOwnTrigger)
            .Select(e => $"{e.Schema}.{e.Table}");

    // U-A7
    [Fact]
    public void U_A7_EveryForbiddenTable_IsInTheMigrationsLiteralList_OrIsTheAuditTable_OrIsAttachedByALaterMigration()
    {
        var inMigration = CatEcommerce.Infrastructure.Persistence.Migrations.AddRetentionGuards.TablesAtMigration.Select(t => $"{t.Schema}.{t.Table}").ToHashSet();
        var attachedLater = LaterMigrationsAttaching();

        var uncovered = ForbiddenTables()
            .Where(t => !inMigration.Contains(t) && !attachedLater.Contains(t))
            .ToList();

        Assert.True(uncovered.Count == 0,
            "Forbidden tables with no retention trigger (call RetentionGuardSql.AttachTo in the migration that adds the table, C-ALL-1): " + string.Join(", ", uncovered));
    }

    [Fact]
    public void U_A7_TheMigrationsList_NamesOnlyForbiddenTables_AndNeverTheAuditTable()
    {
        var forbidden = ForbiddenTables().ToHashSet();
        var inMigration = CatEcommerce.Infrastructure.Persistence.Migrations.AddRetentionGuards.TablesAtMigration.Select(t => $"{t.Schema}.{t.Table}").ToList();

        Assert.DoesNotContain(inMigration, t => RetentionGuardSql.DedicatedlyGuardedTables.Any(d => $"{d.Schema}.{d.Table}" == t));
        Assert.Equal(inMigration.Count, inMigration.Distinct().Count());
        Assert.All(inMigration, table => Assert.Contains(table, forbidden));
    }

    /// <summary>Tables a migration after AddRetentionGuards attaches the two triggers to (scanned from the migration sources).</summary>
    private static HashSet<string> LaterMigrationsAttaching()
    {
        var migrations = Path.Combine(HardDeleteSourceScanTests.SrcRoot(), "Infrastructure", "CatEcommerce.Infrastructure", "Persistence", "Migrations");
        var attached = new HashSet<string>(StringComparer.Ordinal);
        var guardsName = Path.GetFileName(Directory.GetFiles(migrations, "*_AddRetentionGuards.cs").Single());

        foreach (var path in Directory.GetFiles(migrations, "*.cs")
                     .Where(p => string.CompareOrdinal(Path.GetFileName(p), guardsName) > 0 && !p.EndsWith(".Designer.cs", StringComparison.Ordinal)))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(path), "AttachTo\\(\\s*\"(?<s>[a-z_]+)\"\\s*,\\s*\"(?<t>[a-z_]+)\"\\s*\\)"))
                attached.Add($"{m.Groups["s"].Value}.{m.Groups["t"].Value}");
        }

        return attached;
    }
}
