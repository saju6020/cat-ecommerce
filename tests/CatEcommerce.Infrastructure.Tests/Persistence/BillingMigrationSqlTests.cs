using System.Reflection;
using System.Text.RegularExpressions;
using CatEcommerce.Infrastructure.Billing;
using CatEcommerce.Infrastructure.Persistence.Migrations;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Persistence;

/// <summary>
/// HO-BIL-001 §15 U-X3 -- the hand-written SQL of the Billing migrations: the immutability triggers use the enumerations'
/// own <c>Value</c>s (never typed as literals), and every Billing table classified <c>Forbidden</c> refuses <c>DELETE</c> in the
/// database (D-6) -- the only deletes the schema permits are the lines of a <i>draft</i> invoice and the two transient
/// owned working sets the classification marks <c>AggregateOnly</c>.
/// </summary>
public sealed partial class BillingMigrationSqlTests
{
    private static IReadOnlyList<Migration> BillingMigrations() =>
        typeof(AddBillingFoundation).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(AddBillingFoundation).Namespace && t.IsSubclassOf(typeof(Migration)) && t.Name.StartsWith("AddBilling", StringComparison.Ordinal))
            .OrderBy(t => t.GetCustomAttribute<Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute>()!.Id, StringComparer.Ordinal)
            .Select(t => (Migration)Activator.CreateInstance(t)!)
            .ToList();

    private static string UpSql() =>
        string.Join("\n", BillingMigrations().SelectMany(m => m.UpOperations.OfType<SqlOperation>()).Select(o => o.Sql));

    [Fact]
    public void U_X3_TheInvoiceTriggers_AreBuiltFromTheInvoiceStatusValues()
    {
        var sql = BillingMigrationSql.InvoiceImmutability();

        Assert.Contains($"OLD.status = {InvoiceStatus.Draft.Value}", sql);
        Assert.Contains($"OLD.status = {InvoiceStatus.Discarded.Value}", sql);
        Assert.Contains($"NEW.status NOT IN ({InvoiceStatus.Issued.Value}, {InvoiceStatus.PartPaid.Value}, {InvoiceStatus.Paid.Value}, {InvoiceStatus.Credited.Value})", sql);
        Assert.Contains($"parent_status = {InvoiceStatus.Draft.Value}", sql);
        Assert.Contains("'paid_total', 'credited_total', 'status'", sql); // the only content columns that may move after issue

        Assert.Contains("trg_invoice_immutable", UpSql()); // and the migration really runs it
    }

    [Fact]
    public void U_X3_EveryEnumeratedStatusValueInTheTriggerSql_IsAMemberOfTheEnumeration()
    {
        var sql = BillingMigrationSql.InvoiceImmutability();
        var values = Enumeration.GetAll<InvoiceStatus>().Select(s => s.Value).ToHashSet();

        foreach (Match match in StatusComparison().Matches(sql))
            Assert.Contains(int.Parse(match.Groups[1].Value), values);
    }

    [Fact]
    public void U_X3_NoBillingMigrationOrConfiguration_TypesANumericStatusLiteral()
    {
        var infrastructure = FindInfrastructureDirectory();
        var files = new List<string>
        {
            Path.Combine(infrastructure, "Billing", "BillingMigrationSql.cs"),
        };

        files.AddRange(Directory.EnumerateFiles(Path.Combine(infrastructure, "Persistence", "Migrations"), "*AddBilling*.cs").Where(f => !f.EndsWith(".Designer.cs")));
        files.AddRange(Directory.EnumerateFiles(Path.Combine(infrastructure, "Persistence", "Configurations"), "*.cs")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), "\"billing\"")));

        Assert.True(files.Count > 3);

        // An EF-generated filter bakes the configuration's literal into the migration text only if the configuration typed it;
        // Billing's filters (charge_id IS NOT NULL AND NOT invoice_discarded ...) name no status at all.
        var offenders = files.Where(f => StatusLiteral().IsMatch(File.ReadAllText(f))).Select(Path.GetFileName).ToList();
        Assert.True(offenders.Count == 0, "A numeric status literal is typed in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void U_X3_EveryForbiddenBillingTable_ThatExists_RefusesDelete()
    {
        var migrations = BillingMigrations();
        var existing = migrations.SelectMany(m => m.UpOperations.OfType<CreateTableOperation>())
            .Where(t => t.Schema == "billing").Select(t => t.Name).ToHashSet();
        Assert.NotEmpty(existing);

        var sql = UpSql();
        var guarded = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in DeleteGuard().Matches(sql)) guarded.Add(match.Groups[1].Value);

        var forbidden = RecordClassification.All
            .Where(c => c.Schema == "billing" && c.HardDeletePolicy == HardDeletePolicy.Forbidden && c.Table != "invoice_line" && existing.Contains(c.Table))
            .Select(c => c.Table)
            .ToList();

        Assert.NotEmpty(forbidden);
        var unguarded = forbidden.Where(t => !guarded.Contains(t)).ToList();
        Assert.True(unguarded.Count == 0, "These Forbidden tables accept DELETE: " + string.Join(", ", unguarded));

        // The only tables allowed to accept a delete are the AggregateOnly working sets.
        var allowed = RecordClassification.All.Where(c => c.Schema == "billing" && c.HardDeletePolicy == HardDeletePolicy.AggregateOnly).Select(c => c.Table).ToHashSet();
        Assert.All(guarded, t => Assert.DoesNotContain(t, allowed));
    }

    [Fact]
    public void TheNoDeleteAndAppendOnlyHelpers_RaiseP0001_AndExemptOnlyBookkeepingColumns()
    {
        var appendOnly = BillingMigrationSql.AppendOnly("credit_note", "released_amount");
        Assert.Contains("ERRCODE = 'P0001'", appendOnly);
        Assert.Contains("TG_OP = 'DELETE'", appendOnly);
        Assert.Contains("'Version', 'LastUpdatedDate', 'LastUpdatedBy', 'released_amount'", appendOnly);

        var noDelete = BillingMigrationSql.NoDelete("charge");
        Assert.Contains("BEFORE DELETE ON billing.charge", noDelete);
        Assert.Contains("P0001", noDelete);

        var payment = BillingMigrationSql.PaymentImmutability();
        Assert.DoesNotContain("'amount'", payment);       // a payment's amount is a protected core column
        Assert.DoesNotContain("'method'", payment);
        Assert.DoesNotContain("'external_reference'", payment);
        Assert.DoesNotContain("'received_on'", payment);
        Assert.Contains("'status'", payment);              // ...while its status may move
        Assert.Contains("'refunded_total'", payment);
    }

    [Fact]
    public void TheClosedCashUpTrigger_IsBuiltFromTheStatusValue_AndTheMigrationRunsIt()
    {
        var sql = BillingMigrationSql.ClosedCashUpImmutability();

        Assert.Contains($"OLD.status = {CashUpStatus.Closed.Value}", sql);
        Assert.Contains("P0001", sql);
        Assert.Contains("TG_OP = 'DELETE'", sql);
        Assert.Contains("trg_cash_up_closed_immutable", UpSql());
    }

    private static string FindInfrastructureDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Infrastructure", "CatEcommerce.Infrastructure");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/Infrastructure/CatEcommerce.Infrastructure from " + AppContext.BaseDirectory);
    }

    [GeneratedRegex(@"\b(?:OLD\.status|NEW\.status|parent_status)\s*(?:=|<>)\s*(\d+)")]
    private static partial Regex StatusComparison();

    /// <summary><c>status = 1</c>, <c>status &lt;&gt; 5</c>, typed as digits (an interpolated value is not a digit run).</summary>
    [GeneratedRegex(@"\b(status|kind)\s*(=|<>|!=)\s*\d+")]
    private static partial Regex StatusLiteral();

    /// <summary>A trigger that fires on DELETE for a billing table: <c>BEFORE DELETE ON billing.t</c>, <c>BEFORE UPDATE OR DELETE ON
    /// billing.t</c> or <c>BEFORE INSERT OR UPDATE OR DELETE ON billing.t</c> -- except invoice_line, whose trigger lets a draft's
    /// lines go and is therefore not a blanket refusal.</summary>
    [GeneratedRegex(@"BEFORE [A-Z ]*DELETE ON billing\.(?!invoice_line\b)(\w+)")]
    private static partial Regex DeleteGuard();
}
