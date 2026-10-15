using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Read.QueryHandlers.Reporting.Sources;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Application.Tests.QueryHandlers.Reporting;

/// <summary>
/// HO-RPT-001 TEST gate (2026-10-13), T-U3 and T-U4 -- the revenue and payment maths on a SMALL fixture whose expected numbers were worked out by hand BEFORE the test was run, with figures that do not
/// appear in <see cref="FinancialReportBuilderTests"/>. Pure: no database.
/// </summary>
public sealed class ReportMathsFixtureTests
{
    private static readonly DateOnly Day = new(2026, 10, 13);

    private static RevenueFact Invoice(string line, decimal net, decimal tax, bool costed, decimal cogs) =>
        new(Day, "BDT", RevenueFactKind.Invoice, line, "CatFood", "—", "DE", "CatFood", 1m, net, 0m, net, tax, costed ? cogs : 0m, costed);

    private static RevenueFact Credit(string line, decimal net, decimal tax, bool costed) =>
        new(Day, "BDT", RevenueFactKind.Credit, line, "CatFood", "—", "DE", "CatFood", 0m, 0m, 0m, net, tax, 0m, costed);

    // ── T-U3 ────────────────────────────────────────────────────────────────────────────────────────────
    // By hand: Shop = 100 + 40 invoiced, minus a 20 credit note = 120 net. Tax 15 + 6 - 3 = 18. Costed net = 100 - 20 = 80; cost of goods 60; margin 80 - 60 = 20; margin % = 20 / 80 = 25.0;
    // coverage % = 80 / 120 = 66.7. Clinic = 200 net, 30 tax, nothing costed: margin is null, coverage 0.0.
    [Fact]
    public void T_U3_RevenueNetsTheCreditNote_MarginIsOnTheCostedNetOnly_AndAnUncostedLineHasNoMarginNotZero()
    {
        var facts = new[]
        {
            Invoice("Shop", 100m, 15m, costed: true, cogs: 60m),
            Invoice("Shop", 40m, 6m, costed: false, cogs: 0m),
            Invoice("Clinic", 200m, 30m, costed: false, cogs: 0m),
            Credit("Shop", 20m, 3m, costed: true),
        };

        var aggregates = RevenueBuilder.Aggregate(facts, TimeBucket.Day, f => [f.Line]);

        var shop = aggregates.Single(a => a.Dimensions[0] == "Shop");
        Assert.Equal(120m, shop.Net);
        Assert.Equal(20m, shop.CreditNotes);
        Assert.Equal(18m, shop.Tax);
        Assert.Equal(80m, shop.CostedNet);
        Assert.Equal(60m, shop.Cogs);
        Assert.Equal(20m, shop.Margin);
        Assert.Equal(25.0m, shop.MarginPercent);
        Assert.Equal(66.7m, shop.CoveragePercent);

        var clinic = aggregates.Single(a => a.Dimensions[0] == "Clinic");
        Assert.Equal(200m, clinic.Net);
        Assert.Equal(30m, clinic.Tax);
        Assert.Null(clinic.Margin);                  // never zero: nothing of the clinic line is costed
        Assert.Null(clinic.MarginPercent);
        Assert.Equal(0.0m, clinic.CoveragePercent);

        // Across both lines the totals are the sums of the rows, not a recomputation: net 320, credit notes 20.
        Assert.Equal(320m, aggregates.Sum(a => a.Net));
        Assert.Equal(20m, aggregates.Sum(a => a.CreditNotes));
    }

    [Fact]
    public void T_U3_ACreditNoteInAnotherCurrency_IsNeverNettedAgainstTheOtherCurrencysRevenue()
    {
        var usd = new RevenueFact(Day, "USD", RevenueFactKind.Credit, "Shop", "CatFood", "—", "DE", "CatFood", 0m, 0m, 0m, 5m, 0m, 0m, false);

        var aggregates = RevenueBuilder.Aggregate([Invoice("Shop", 100m, 0m, false, 0m), usd], TimeBucket.Day, f => [f.Line]);

        Assert.Equal(100m, aggregates.Single(a => a.Currency == "BDT").Net);
        Assert.Equal(-5m, aggregates.Single(a => a.Currency == "USD").Net);
    }

    // ── T-U4 ────────────────────────────────────────────────────────────────────────────────────────────
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly IReadOnlyDictionary<Guid, string> BranchNames = new Dictionary<Guid, string> { [Branch] = "Gulshan" };

    private static RvPaymentRow Payment(PaymentMethod method, decimal amount, decimal remitted = 0m, bool matched = false) => new()
    {
        PaymentId = Guid.NewGuid(), BranchId = Branch, Method = method.Value, Amount = amount, CurrencyCode = "BDT", Status = PaymentStatus.Accepted.Value, BusinessDate = Day,
        ReceivedOn = Day, RemittedAmount = remitted, BankMatched = matched, CollectorKind = method == PaymentMethod.CashOnDelivery ? CollectorKind.Courier.Value : null,
    };

    // By hand: COD expected 1000 + 600 = 1600, received 1000 + 450 = 1450, variance 150, one remitted in full, one short. Transfer expected 500 + 300 = 800, received (matched only) 500, variance 300.
    // Card expected 200 + 150 = 350, no settlement figure. Cash with no cash-up: the day's takings 120 as expected, nothing counted.
    [Fact]
    public void T_U4_Payments_AreReconciledPerMethod_OnlyTheMatchedOrRemittedAmountIsReceived()
    {
        var payments = new[]
        {
            Payment(PaymentMethod.CashOnDelivery, 1000m, remitted: 1000m),
            Payment(PaymentMethod.CashOnDelivery, 600m, remitted: 450m),
            Payment(PaymentMethod.BankTransfer, 500m, matched: true),
            Payment(PaymentMethod.BankTransfer, 300m),
            Payment(PaymentMethod.CardInStore, 200m),
            Payment(PaymentMethod.CardInStore, 150m),
            Payment(PaymentMethod.CashInStore, 120m),
        };

        var (rows, notes) = PaymentReconciliationBuilder.Build(payments, [], BranchNames, "BDT");

        var cod = rows.Single(r => (string)r["method"]! == "CashOnDelivery");
        Assert.Equal(new ReportMoney(1600m, "BDT"), cod["expected"]);
        Assert.Equal(new ReportMoney(1450m, "BDT"), cod["received"]);
        Assert.Equal(new ReportMoney(150m, "BDT"), cod["variance"]);
        Assert.Equal(1L, cod["matched"]);
        Assert.Equal(1L, cod["mismatchesOpen"]);

        var transfer = rows.Single(r => (string)r["method"]! == "BankTransfer");
        Assert.Equal(new ReportMoney(800m, "BDT"), transfer["expected"]);
        Assert.Equal(new ReportMoney(500m, "BDT"), transfer["received"]);
        Assert.Equal(new ReportMoney(300m, "BDT"), transfer["variance"]);
        Assert.Equal(1L, transfer["matched"]);
        Assert.Equal(1L, transfer["mismatchesOpen"]);

        var card = rows.Single(r => (string)r["method"]! == "CardInStore");
        Assert.Equal(new ReportMoney(350m, "BDT"), card["expected"]);
        Assert.Null(card["received"]);
        Assert.Null(card["variance"]);

        var cash = rows.Single(r => (string)r["method"]! == "CashInStore");
        Assert.Equal(new ReportMoney(120m, "BDT"), cash["expected"]);
        Assert.Null(cash["received"]);              // the day was never counted: blank, not zero
        Assert.Null(cash["variance"]);

        Assert.Equal(4, rows.Count);
        Assert.Single(notes);
    }

    [Fact]
    public void T_U4_ARemittanceAboveThePayment_IsCappedAtThePayment_SoTheVarianceCannotGoNegative()
    {
        var (rows, _) = PaymentReconciliationBuilder.Build([Payment(PaymentMethod.CashOnDelivery, 500m, remitted: 800m)], [], BranchNames, "BDT");

        var cod = Assert.Single(rows);
        Assert.Equal(new ReportMoney(500m, "BDT"), cod["expected"]);
        Assert.Equal(new ReportMoney(500m, "BDT"), cod["received"]);
        Assert.Equal(new ReportMoney(0m, "BDT"), cod["variance"]);
    }
}
