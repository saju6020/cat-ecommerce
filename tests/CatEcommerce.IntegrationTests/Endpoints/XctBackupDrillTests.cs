using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>A fact that runs only when <c>RUN_BACKUP_DRILL=1</c>: the drill needs Docker, image pulls and about 15 minutes, so it is not in the default suite (section 16.2).</summary>
public sealed class BackupDrillFactAttribute : FactAttribute
{
    public BackupDrillFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_BACKUP_DRILL") != "1")
            Skip = "Set RUN_BACKUP_DRILL=1 (needs Docker and the postgres:17-bookworm image). Category=BackupDrill, HO-XCT-001 section 16.2.";
    }
}

/// <summary>
/// HO-XCT-001 section 16.2 I-G1..I-G6 and I-G8 through <c>deploy/backup/test/run-drill-test.sh</c>, and I-G4's second half: the drill JSON is posted to the real
/// restore-test endpoint, which must accept it as it is and compute <c>Passed</c>. Run with <c>RUN_BACKUP_DRILL=1 dotnet test --filter Category=BackupDrill</c>.
/// </summary>
[Trait("Category", "BackupDrill")]
[Collection("XctRetention")]
public sealed class XctBackupDrillTests(XctApiFactory factory) : XctIntegrationTestBase(factory)
{
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CatEcommerce.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }

    [BackupDrillFact]
    public async Task TheDrill_PassesEveryCase_AndItsJsonIsAcceptedByTheRestoreTestEndpointAsPassed()
    {
        // BACKUP_DRILL_RESULTS=<dir of an earlier run-drill-test.sh>: judge those results instead of running the 15-minute drill again.
        var reuse = Environment.GetEnvironmentVariable("BACKUP_DRILL_RESULTS");
        var output = reuse ?? Path.Combine(Path.GetTempPath(), "purrcare-drill-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Task<string> stdout = Task.FromResult(string.Empty), stderr = Task.FromResult(string.Empty);
        var exitCode = 0;
        if (reuse is null)
        {
            var start = new ProcessStartInfo("bash", $"\"{Path.Combine(RepositoryRoot(), "deploy", "backup", "test", "run-drill-test.sh").Replace('\\', '/')}\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = RepositoryRoot()
            };
            start.Environment["OUT_DIR"] = output.Replace('\\', '/');

            using var process = Process.Start(start)!;
            stderr = process.StandardError.ReadToEndAsync();
            stdout = process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            exitCode = process.ExitCode;
        }

        var results = File.ReadAllText(Path.Combine(output, "results.txt"));
        Assert.True(exitCode == 0, results + "\n" + await stderr);
        Assert.DoesNotContain("FAIL ", results);
        Assert.Contains("PASS I-G2", results);
        Assert.Contains("PASS I-G3", results);
        Assert.Contains("PASS I-G5", results);
        Assert.Contains("PASS I-G6", results);

        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);
        var drill = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "drill.json"))).RootElement;
        var response = await admin.PostAsJsonAsync("/api/admin/operations/restore-tests", drill, Json);

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var recorded = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Passed", recorded.GetProperty("outcome").GetString());
        await stdout;
    }
}

/// <summary>HO-XCT-001 I-G7 and the section 19 CODE_REVIEW focus -- static properties of <c>deploy/</c>, run in the default suite (no Docker).</summary>
public sealed class DeployConventionTests
{
    private static string DeployRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CatEcommerce.sln"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "deploy");
    }

    private static IEnumerable<string> DeployFiles() =>
        Directory.EnumerateFiles(DeployRoot(), "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    // I-G7
    [Fact]
    public void I_G7_NoFileUnderDeploy_HoldsALiteralSecret_OnlyPlaceholdersAndEnvironmentReferences()
    {
        // name = value, where the name says secret and the value is something other than a ${VAR}, $VAR or "$(...)" reference or empty.
        var literal = new Regex(@"(?i)(password|passphrase|secret|cipher-pass|s3-key)\w*\s*=\s*(?![""']?\$)(?![""']?\s*$)[^\s#]+");
        var offenders = new List<string>();

        foreach (var file in DeployFiles())
        {
            foreach (var (line, number) in File.ReadLines(file).Select((l, i) => (l, i + 1)))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith('#') || trimmed.StartsWith("--")) continue;
                if (literal.IsMatch(line)) offenders.Add($"{Path.GetRelativePath(DeployRoot(), file)}:{number}: {line.Trim()}");
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryShellScript_StartsWithABashShebang_SetsErrexitNounsetPipefail_AndTheDeployTreeIsLfOnly()
    {
        var scripts = DeployFiles().Where(f => f.EndsWith(".sh", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(scripts);

        foreach (var script in scripts)
        {
            var text = File.ReadAllText(script);
            Assert.StartsWith("#!/usr/bin/env bash", text);
            Assert.Contains("set -euo pipefail", text);
            Assert.DoesNotContain('\r', text);
        }
    }

    [Fact]
    public void TheRestoreScripts_RefuseToRunWithoutTheirSafetyFlags()
    {
        var pitr = File.ReadAllText(Path.Combine(DeployRoot(), "backup", "scripts", "restore-pitr.sh"));
        Assert.Contains("--confirm", pitr);
        Assert.Contains("--i-understand-this-replaces-production", pitr);
        Assert.Contains("--delta", pitr);

        var drill = File.ReadAllText(Path.Combine(DeployRoot(), "backup", "scripts", "restore-drill.sh"));
        Assert.Contains("DRILL_ENVIRONMENT", drill);
    }

    [Fact]
    public void TheArchiveSettings_AreTheOnesTheRpoArgumentRestsOn()
    {
        var conf = File.ReadAllText(Path.Combine(DeployRoot(), "backup", "postgresql.backup.conf"));
        Assert.Matches(@"archive_timeout\s*=\s*60\b", conf);
        Assert.Contains("archive_mode = on", conf);
        Assert.Contains("archive-push", conf);

        var pgbackrest = File.ReadAllText(Path.Combine(DeployRoot(), "backup", "pgbackrest.conf.template"));
        Assert.Contains("repo2-cipher-type=aes-256-cbc", pgbackrest);
        Assert.Contains("repo1-retention-full=5", pgbackrest);
        Assert.Contains("repo2-retention-full=5", pgbackrest);
    }

    [Fact]
    public void TheJournalExport_SelectsIdsAndDatesOnly()
    {
        var script = File.ReadAllText(Path.Combine(DeployRoot(), "backup", "scripts", "export-erasure-journal.sh"));
        Assert.Contains("customer_account_id, requested_on FROM ops.erasure_request", script);
        Assert.DoesNotContain("SELECT *", script, StringComparison.OrdinalIgnoreCase);
    }
}
