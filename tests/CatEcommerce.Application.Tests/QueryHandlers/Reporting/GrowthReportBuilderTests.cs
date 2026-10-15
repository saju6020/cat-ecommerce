using CatEcommerce.Domain.Models;
using CatEcommerce.Read.QueryHandlers.Reporting.Sources;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Application.Tests.QueryHandlers.Reporting;

/// <summary>HO-RPT-001 section 15 F -- U-F1 (new, returning and registrations, ADR-291) and U-F2 (the cohort matrix), plus the lifetime-value bands. Pure.</summary>
public sealed class GrowthReportBuilderTests
{
    private static readonly TimeSpan Dhaka = TimeSpan.FromHours(6);
    private static DateTimeOffset At(int year, int month, int day) => new(year, month, day, 10, 0, 0, Dhaka);
    private static DateOnly Local(DateTimeOffset at) => DateOnly.FromDateTime(at.ToOffset(Dhaka).DateTime);

    private static ActivityEvent Invoice(Guid customer, DateTimeOffset at, int line = 2, decimal net = 100m) => new(customer, at, line, net, false);

    // ── U-F1 ─────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_F1_ACustomerWhoseFirstInvoiceIsInThePeriodIsNew_OneWithAnEarlierInvoiceReturns_AndRegistrationsAreCountedApart()
    {
        var fresh = Guid.NewGuid();
        var returning = Guid.NewGuid();
        var onlyRegistered = Guid.NewGuid();
        var inRange = new[] { Invoice(fresh, At(2026, 10, 5)), Invoice(returning, At(2026, 10, 7)), Invoice(returning, At(2026, 10, 20)) };
        var firstEver = new Dictionary<Guid, DateTimeOffset> { [fresh] = At(2026, 10, 5), [returning] = At(2026, 9, 2) };
        var channels = new Dictionary<Guid, int> { [fresh] = RegistrationChannel.Reception.Value, [returning] = RegistrationChannel.SelfService.Value, [onlyRegistered] = RegistrationChannel.SelfService.Value };
        var registrations = new[] { (At(2026, 10, 5), RegistrationChannel.Reception.Value), (At(2026, 10, 12), RegistrationChannel.SelfService.Value) };

        var (rows, totals) = NewVersusReturningBuilder.Build(inRange, firstEver, channels, registrations, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), TimeBucket.Month, Local);

        var october = Assert.Single(rows);
        Assert.Equal("2026-10", october["bucket"]);
        Assert.Equal(2L, october["activeCustomers"]);
        Assert.Equal(1L, october["newCustomers"]);
        Assert.Equal(1L, october["returningCustomers"]);
        Assert.Equal(1L, october["newReception"]);
        Assert.Equal(0L, october["newSelfService"]);
        Assert.Equal(2L, october["registrations"]); // the one who only registered counts here, not as active
        Assert.Equal(2L, totals["activeCustomers"]);
    }

    [Fact]
    public void U_F1_ACreditNoteIsNotActivity()
    {
        var customer = Guid.NewGuid();
        var (rows, _) = NewVersusReturningBuilder.Build(
            [new ActivityEvent(customer, At(2026, 10, 5), 2, -50m, true)], new Dictionary<Guid, DateTimeOffset>(), new Dictionary<Guid, int>(), [],
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), TimeBucket.Month, Local);

        Assert.Equal(0L, Assert.Single(rows)["activeCustomers"]);
    }

    // ── U-F2 ─────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_F2_TheCohortMatrix_HasTheRightOffsets_AndIncompleteMonthsAreNull()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var firstEver = new Dictionary<Guid, DateTimeOffset> { [a] = At(2026, 1, 10), [b] = At(2026, 1, 20), [c] = At(2026, 2, 3) };
        var invoices = new[]
        {
            Invoice(a, At(2026, 1, 10)), Invoice(b, At(2026, 1, 20)), Invoice(c, At(2026, 2, 3)),
            Invoice(a, At(2026, 2, 15)),                    // January cohort retained at month 1: 1 of 2
            Invoice(a, At(2026, 4, 2)), Invoice(b, At(2026, 4, 9)), // retained at month 3: 2 of 2
            Invoice(c, At(2026, 3, 1)),                      // February cohort retained at month 1: 1 of 1
        };

        var rows = CohortRetentionBuilder.Build(invoices, firstEver, "AnyLine", new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28), new DateOnly(2026, 4, 20), Local);

        var january = rows.Single(r => (string)r["cohortMonth"]! == "2026-01");
        Assert.Equal(2L, january["cohortSize"]);
        Assert.Equal(50.0m, january["month1"]);
        Assert.Equal(0.0m, january["month2"]);
        Assert.Equal(100.0m, january["month3"]);
        Assert.Null(january["month4"]); // May has not happened by 20 April 2026
    }

    [Fact]
    public void U_F2_MonthsThatHaveNotHappenedYet_AreNull_AndTheMeasureNarrowsTheLine()
    {
        var a = Guid.NewGuid();
        var firstEver = new Dictionary<Guid, DateTimeOffset> { [a] = At(2026, 1, 10) };
        var invoices = new[] { Invoice(a, At(2026, 1, 10), line: BusinessLine.Clinic.Value), Invoice(a, At(2026, 2, 10), line: BusinessLine.Shop.Value) };
        var today = new DateOnly(2026, 3, 15);

        var any = CohortRetentionBuilder.Build(invoices, firstEver, "AnyLine", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), today, Local).Single();
        var purchase = CohortRetentionBuilder.Build(invoices, firstEver, "RepeatPurchase", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), today, Local).Single();
        var visit = CohortRetentionBuilder.Build(invoices, firstEver, "RepeatVisit", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), today, Local).Single();

        Assert.Equal(100.0m, any["month1"]);
        Assert.Equal(100.0m, purchase["month1"]); // the shop invoice in February
        Assert.Equal(0.0m, visit["month1"]);      // no clinic invoice in February
        Assert.Equal(0.0m, any["month2"]);        // March exists (it is 15 March)
        Assert.Null(any["month3"]);               // April has not happened
        Assert.Null(any["month12"]);
    }

    // ── Lifetime value ───────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void TheLifetimeValue_IsInvoicesLessCreditNotes_AcrossLines_InBands_WithTheSharesPerLine_AndTheTopCustomers()
    {
        var big = Guid.NewGuid();
        var small = Guid.NewGuid();
        var events = new[]
        {
            Invoice(big, At(2026, 10, 1), BusinessLine.Clinic.Value, 3000m), Invoice(big, At(2026, 10, 2), BusinessLine.Shop.Value, 1000m), new ActivityEvent(big, At(2026, 10, 3), BusinessLine.Shop.Value, -200m, true),
            Invoice(small, At(2026, 10, 4), BusinessLine.Shop.Value, 400m),
        };

        var rows = LifetimeValueBuilder.Build(events, new Dictionary<Guid, string> { [big] = "Big Owner", [small] = "Small Owner" }, "BDT");

        var bands = rows.Where(r => (string)r["section"]! == "Band").ToList();
        Assert.Equal(["Under 1,000", "1,000 to 4,999"], bands.Select(r => (string)r["label"]!));
        var upper = bands[1];
        Assert.Equal(new ReportMoney(3800m, "BDT"), upper["averageLtv"]);
        Assert.Equal(78.9m, upper["clinicShare"]);   // 3000 / 3800
        Assert.Equal(21.1m, upper["shopShare"]);     // 800 / 3800

        var top = rows.Where(r => (string)r["section"]! == "Top 50").ToList();
        Assert.Equal(["Big Owner", "Small Owner"], top.Select(r => (string)r["label"]!));
        Assert.Equal("Clinic, Shop", top[0]["linesUsed"]);
    }
}
