using System.Globalization;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Read.QueryHandlers.Reporting;
using CatEcommerce.Read.QueryHandlers.Reporting.Sources;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Application.Tests.QueryHandlers.Reporting;

/// <summary>HO-RPT-001-FU1 -- the server-computed <c>varianceDirection</c> of R13 (payment reconciliation) and R14 (daily cash-up), at the zero and one-cent boundaries. All pure: no database.</summary>
public sealed class VarianceDirectionTests
{
    private static readonly DateOnly Day = new(2026, 10, 14);
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly IReadOnlyDictionary<Guid, string> BranchNames = new Dictionary<Guid, string> { [Branch] = "Gulshan" };

    private static decimal D(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);

    // counted, expected, the stored variance cell (counted - expected, as Billing writes it), the direction
    public static TheoryData<string, string, string, string> Boundaries => new()
    {
        { "100", "100", "0", "Matched" },
        { "100.00", "100.00", "0.00", "Matched" },
        { "100.00", "100.00", "-0.00", "Matched" },
        { "0", "0", "0", "Matched" },
        { "-0.00", "0.00", "-0.00", "Matched" },
        { "100.01", "100.00", "0.01", "Over" },
        { "99.99", "100.00", "-0.01", "Short" },
        { "0.01", "0", "0.01", "Over" },
        { "0", "0.01", "-0.01", "Short" },
    };

    [Theory]
    [MemberData(nameof(Boundaries))]
    public void R14_TheDirection_IsCountedAgainstExpected_AtTheBoundaries(string counted, string expected, string variance, string direction)
    {
        var record = new RvCashUpRow { BranchId = Branch, BusinessDate = Day, Status = CashUpStatus.Closed.Value, ExpectedCash = D(expected), CountedCash = D(counted), Variance = D(variance) };

        var rows = CashUpReportBuilder.Build([record], BranchNames, new Dictionary<Guid, string>(), Day, Day, Day, Branch, "BDT");

        Assert.Equal(direction, Assert.Single(rows)[VarianceDirections.ColumnKey]);
    }

    [Theory]
    [MemberData(nameof(Boundaries))]
    public void R13_TheCashRow_UsesTheSameDirection_AtTheBoundaries(string counted, string expected, string variance, string direction)
    {
        var record = new RvCashUpRow { BranchId = Branch, BusinessDate = Day, Status = CashUpStatus.Closed.Value, ExpectedCash = D(expected), CountedCash = D(counted), Variance = D(variance) };

        var (rows, _) = PaymentReconciliationBuilder.Build([], [record], BranchNames, "BDT");

        Assert.Equal(direction, Assert.Single(rows)[VarianceDirections.ColumnKey]);
    }

    [Fact]
    public void R14_ADayNotClosed_AndADayNotYetCounted_HaveNoDirection_NeverMatched()
    {
        var notCounted = new RvCashUpRow { BranchId = Branch, BusinessDate = Day, Status = CashUpStatus.Open.Value, ExpectedCash = 100m, CountedCash = null, Variance = null };

        var rows = CashUpReportBuilder.Build([notCounted], BranchNames, new Dictionary<Guid, string>(), Day.AddDays(-1), Day, Day, Branch, "BDT");

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Null(r[VarianceDirections.ColumnKey]));
    }

    // R13 cash on delivery and bank transfer rows carry expected - received, the OPPOSITE sign to the cash row: the direction must not follow the sign of the variance cell.
    [Theory]
    [InlineData("100.00", "100.00", "Matched")]
    [InlineData("100.00", "99.99", "Short")]
    [InlineData("0.01", "0", "Short")]
    public void R13_CashOnDelivery_IsShortWhenLessWasRemitted_EvenThoughItsVarianceIsPositive(string amount, string remitted, string direction)
    {
        var payment = Payment(ReportPaymentMethods.CashOnDelivery.Value, D(amount), remitted: D(remitted), collector: CollectorKind.Courier.Value);

        var (rows, _) = PaymentReconciliationBuilder.Build([payment], [], BranchNames, "BDT");

        var row = Assert.Single(rows);
        Assert.Equal(direction, row[VarianceDirections.ColumnKey]);
        Assert.Equal(new ReportMoney(D(amount) - D(remitted), "BDT"), row["variance"]); // the value is unchanged: expected - received
    }

    [Fact]
    public void R13_BankTransfer_IsShortWhenNotMatched_AndMatchedWhenMatched()
    {
        var matched = Payment(ReportPaymentMethods.BankTransfer.Value, 100m, matched: true);
        var unmatched = Payment(ReportPaymentMethods.BankTransfer.Value, 0.01m);

        var (rows, _) = PaymentReconciliationBuilder.Build([matched, unmatched], [], BranchNames, "BDT");

        var row = Assert.Single(rows); // one group: day + branch + currency
        Assert.Equal("Short", row[VarianceDirections.ColumnKey]); // 100.01 expected, 100.00 received
        Assert.Equal(new ReportMoney(0.01m, "BDT"), row["variance"]);

        var (onlyMatched, _) = PaymentReconciliationBuilder.Build([matched], [], BranchNames, "BDT");
        Assert.Equal("Matched", Assert.Single(onlyMatched)[VarianceDirections.ColumnKey]);
    }

    [Fact]
    public void R13_CardInStore_AndAnUncountedCashDay_HaveNoDirection()
    {
        var card = Payment(ReportPaymentMethods.CardInStore.Value, 700m);
        var cash = Payment(ReportPaymentMethods.CashInStore.Value, 300m);

        var (rows, _) = PaymentReconciliationBuilder.Build([card, cash], [], BranchNames, "BDT");

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Null(r[VarianceDirections.ColumnKey]));
    }

    [Fact]
    public void BothReports_DeclareTheColumn_WithItsMeaning_RightAfterVariance()
    {
        foreach (var key in new[] { ReportCatalogue.PaymentReconciliationKey, ReportCatalogue.DailyCashUpKey })
        {
            var columns = ReportCatalogue.Find(key)!.Columns.ToList();
            var at = columns.FindIndex(c => c.Key == "variance");

            Assert.Equal(VarianceDirections.ColumnKey, columns[at + 1].Key);
            Assert.Equal(ReportColumnType.Text, columns[at + 1].Type);
            Assert.Contains("Over", columns[at + 1].Description);
            Assert.Contains("Short", columns[at + 1].Description);
            Assert.Contains("Matched", columns[at + 1].Description);
        }

        Assert.False(ReportCatalogue.Find(ReportCatalogue.PaymentReconciliationKey)!.HasTotals); // no totals row to carry a direction
        Assert.False(ReportCatalogue.Find(ReportCatalogue.DailyCashUpKey)!.HasTotals);
    }

    [Fact]
    public void TheEnum_HasExactlyTheThreeContractValues()
        => Assert.Equal(["Matched", "Over", "Short"], Enum.GetNames<VarianceDirection>().Order());

    private static RvPaymentRow Payment(int method, decimal amount, decimal remitted = 0m, bool matched = false, int? collector = null) => new()
    {
        PaymentId = Guid.NewGuid(), BranchId = Branch, Method = method, Amount = amount, CurrencyCode = "BDT", Status = PaymentStatus.Accepted.Value, BusinessDate = Day,
        ReceivedOn = Day, RemittedAmount = remitted, BankMatched = matched, CollectorKind = collector,
    };
}
