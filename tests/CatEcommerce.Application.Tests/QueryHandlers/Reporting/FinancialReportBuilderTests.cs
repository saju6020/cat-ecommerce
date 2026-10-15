using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Read.QueryHandlers.Reporting.Sources;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Application.Tests.QueryHandlers.Reporting;

/// <summary>HO-RPT-001 section 15 D -- U-D1 to U-D7: business line, revenue, margin and coverage, the four payment methods, ageing and currencies. All pure: no database.</summary>
public sealed class FinancialReportBuilderTests
{
    private static readonly DateOnly October = new(2026, 10, 14);
    private static readonly DateOnly November = new(2026, 11, 2);

    private static RevenueFact Invoice(DateOnly date, string line, decimal net, decimal tax = 0m, bool costed = false, decimal cogs = 0m, string currency = "BDT", string category = "CatFood", string origin = "DE") =>
        new(date, currency, RevenueFactKind.Invoice, line, category, "—", origin, category, 1m, net, 0m, net, tax, costed ? cogs : 0m, costed);

    private static RevenueFact Credit(DateOnly date, string line, decimal net, decimal tax = 0m, bool costed = false, string currency = "BDT") =>
        new(date, currency, RevenueFactKind.Credit, line, "CatFood", "—", "DE", "CatFood", 0m, 0m, 0m, net, tax, 0m, costed);

    // ── U-D1 ───────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D1_TheBusinessLine_IsTheOneBillingStoredOnTheLine_AndAnythingElseIsOther_NeverDropped()
    {
        Assert.Equal("Clinic", BusinessLineRules.Classify(BusinessLine.Clinic.Value));
        Assert.Equal("Shop", BusinessLineRules.Classify(BusinessLine.Shop.Value));
        Assert.Equal("Boarding", BusinessLineRules.Classify(BusinessLine.Boarding.Value));
        Assert.Equal("Other", BusinessLineRules.Classify(99)); // a line a later area adds

        Assert.All(Enumeration.GetAll<BusinessLine>(), b => Assert.NotEqual("Other", BusinessLineRules.Classify(b.Value)));

        // A synthetic unknown line is aggregated under Other: shown, not dropped.
        var aggregates = RevenueBuilder.Aggregate([Invoice(October, BusinessLineRules.Classify(99), 40m)], TimeBucket.Month, f => [f.Line]);
        Assert.Equal("Other", aggregates.Single().Dimensions[0]);
        Assert.Equal(40m, aggregates.Single().Net);
    }

    // ── U-D2 ───────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D2_OnlyIssuedPartPaidPaidAndCreditedInvoicesCount_ADraftOrDiscardedNeverDoes()
    {
        Assert.Equivalent(new[] { InvoiceStatus.Issued.Value, InvoiceStatus.PartPaid.Value, InvoiceStatus.Paid.Value, InvoiceStatus.Credited.Value }, RevenueStatuses.Counted);
        Assert.DoesNotContain(InvoiceStatus.Draft.Value, RevenueStatuses.Counted);
        Assert.DoesNotContain(InvoiceStatus.Discarded.Value, RevenueStatuses.Counted);
    }

    [Fact]
    public void U_D2_ACreditNote_IsNegativeInItsOwnMonth_AndTaxIsNotInNetRevenue()
    {
        var facts = new[]
        {
            Invoice(October, "Shop", 1000m, tax: 150m),
            Invoice(October, "Clinic", 400m, tax: 60m),
            Credit(November, "Shop", 250m, tax: 37.5m),
        };

        var aggregates = RevenueBuilder.Aggregate(facts, TimeBucket.Month, f => [f.Line]);

        var octoberShop = aggregates.Single(a => a.Bucket == "2026-10" && a.Dimensions[0] == "Shop");
        Assert.Equal(1000m, octoberShop.Net);
        Assert.Equal(150m, octoberShop.Tax);
        Assert.Equal(0m, octoberShop.CreditNotes);

        var novemberShop = aggregates.Single(a => a.Bucket == "2026-11" && a.Dimensions[0] == "Shop");
        Assert.Equal(-250m, novemberShop.Net); // the credit note reduces the month it is issued in; October never changes
        Assert.Equal(250m, novemberShop.CreditNotes);
        Assert.Equal(-37.5m, novemberShop.Tax);
    }

    // ── U-D3: margin only where the landed cost is known; coverage always stated ─────────────────────────
    [Fact]
    public void U_D3_MarginIsOnTheCostedLineOnly_CoverageIsCostedOverTotal_AndZeroCoverageMeansNullNotZero()
    {
        var facts = new[]
        {
            Invoice(October, "Shop", 100m, costed: true, cogs: 60m),   // fully costed
            Invoice(October, "Shop", 100m, costed: false),            // one batch has no landed cost: the whole line is uncosted
        };

        var aggregate = RevenueBuilder.Aggregate(facts, TimeBucket.Month, f => [f.Line]).Single();

        Assert.Equal(200m, aggregate.Net);
        Assert.Equal(100m, aggregate.CostedNet);
        Assert.Equal(40m, aggregate.Margin); // 100 - 60, on the costed line only
        Assert.Equal(40.0m, aggregate.MarginPercent);
        Assert.Equal(50.0m, aggregate.CoveragePercent);

        var coverage = RevenueBuilder.Coverage([aggregate])!;
        Assert.Equal("Margin covers 50.0 % of shop revenue. 50.0 % came from batches with no landed cost yet.", RevenueBuilder.CoverageNote(coverage, "shop")!.Text);

        var none = RevenueBuilder.Aggregate([Invoice(October, "Shop", 100m, costed: false)], TimeBucket.Month, f => [f.Line]).Single();
        Assert.Null(none.Margin);        // "Not available", never zero
        Assert.Null(none.MarginPercent);
        Assert.Equal(0.0m, none.CoveragePercent);
        Assert.Contains("not available", RevenueBuilder.CoverageNote(RevenueBuilder.Coverage([none]), "shop")!.Text);

        var all = RevenueBuilder.Aggregate([Invoice(October, "Shop", 100m, costed: true, cogs: 70m)], TimeBucket.Month, f => [f.Line]).Single();
        Assert.Null(RevenueBuilder.CoverageNote(RevenueBuilder.Coverage([all]), "shop")); // nothing to caveat
    }

    [Fact]
    public void U_D3_ACreditOnACostedLineReducesTheCostedRevenue_AndAReturnReducesTheCostInItsOwnPeriod()
    {
        var facts = new[]
        {
            Invoice(October, "Shop", 200m, costed: true, cogs: 120m),
            Credit(November, "Shop", 100m, costed: true),
            new RevenueFact(November, "BDT", RevenueFactKind.ReturnCost, "Shop", "CatFood", "—", "DE", "CatFood", 0m, 0m, 0m, 0m, 0m, 60m, true),
        };

        var aggregates = RevenueBuilder.Aggregate(facts, TimeBucket.Month, f => [f.Line]);

        var october = aggregates.Single(a => a.Bucket == "2026-10");
        Assert.Equal(80m, october.Margin); // 200 - 120

        var november = aggregates.Single(a => a.Bucket == "2026-11");
        Assert.Equal(-100m, november.Net);
        Assert.Equal(-100m, november.CostedNet);
        Assert.Equal(-60m, november.Cogs);
        Assert.Null(november.Margin); // nothing was invoiced and costed in November
    }

    // ── The cost of one line ───────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void TheCostOfALine_IsTheSaleEntriesAtLandedCost_AllOrNothing_AndAReturnIsSubtractedOnItsDate()
    {
        var at = DateTimeOffset.Parse("2026-10-14T10:00:00Z");
        LedgerCostEntry Sale(decimal qty, decimal? cost) => new(MovementKind.Sale.Value, -qty, at, cost);

        var costed = LineCosting.Cost([Sale(3m, 10m), Sale(2m, 12m), new LedgerCostEntry(MovementKind.Return.Value, 1m, at.AddDays(20), 12m)]);
        Assert.True(costed.Costed);
        Assert.Equal(54m, costed.SaleCogs); // 3 x 10 + 2 x 12
        Assert.Equal((at.AddDays(20), 12m), Assert.Single(costed.Returns));

        Assert.False(LineCosting.Cost([Sale(3m, 10m), Sale(2m, null)]).Costed); // one uncosted batch: the whole line is uncosted
        Assert.False(LineCosting.Cost([]).Costed);                              // no stock movement (a service, a delivery fee): nothing to cost
        Assert.False(LineCosting.Cost([new LedgerCostEntry(MovementKind.Receipt.Value, 5m, at, 10m)]).Costed); // a receipt is not a sale
        Assert.Equal(LineCost.Uncosted, LineCosting.Cost([Sale(1m, null)]));
    }

    // ── U-D5: exactly the four launch methods ─────────────────────────────────────────────────────────────
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly IReadOnlyDictionary<Guid, string> BranchNames = new Dictionary<Guid, string> { [Branch] = "Gulshan" };

    private static RvPaymentRow Payment(PaymentMethod method, decimal amount, decimal remitted = 0m, bool matched = false, CollectorKind? collector = null) => new()
    {
        PaymentId = Guid.NewGuid(), BranchId = Branch, Method = method.Value, Amount = amount, CurrencyCode = "BDT", Status = PaymentStatus.Accepted.Value, BusinessDate = October,
        ReceivedOn = October, RemittedAmount = remitted, BankMatched = matched, CollectorKind = (collector ?? (method == PaymentMethod.CashOnDelivery ? CollectorKind.Courier : null))?.Value,
    };

    [Fact]
    public void U_D5_TheMethodRows_AreOnlyTheFourLaunchMethods_TheCardRowHasTheSettlementNote_AndThereIsNoGatewayRow()
    {
        var payments = new[]
        {
            Payment(PaymentMethod.CashOnDelivery, 1000m, remitted: 800m),   // a courier shortfall of 200
            Payment(PaymentMethod.BankTransfer, 500m, matched: true),
            Payment(PaymentMethod.CashInStore, 300m),
            Payment(PaymentMethod.CardInStore, 700m),
        };
        var cashUps = new[] { new RvCashUpRow { BranchId = Branch, BusinessDate = October, Status = CashUpStatus.Closed.Value, ExpectedCash = 300m, CountedCash = 290m, Variance = -10m } };

        var (rows, notes) = PaymentReconciliationBuilder.Build(payments, cashUps, BranchNames, "BDT");

        Assert.Equal(["CashOnDelivery", "BankTransfer", "CashInStore", "CardInStore"], rows.Select(r => (string)r["method"]!));
        Assert.Equal(PaymentMethod.CashOnDelivery.DisplayName, rows[0]["method"]);
        Assert.DoesNotContain(rows, r => ((string)r["method"]!).Contains("Gateway", StringComparison.OrdinalIgnoreCase) || ((string)r["method"]!).Contains("Online", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(new[] { "CashOnDelivery", "BankTransfer", "CashInStore", "CardInStore" }.Order(), Enumeration.GetAll<PaymentMethod>().Select(m => m.DisplayName).Order()); // the enumeration itself has no gateway member

        // COD against the courier: expected 1000, remitted 800, variance 200, one mismatch open.
        Assert.Equal(new ReportMoney(1000m, "BDT"), rows[0]["expected"]);
        Assert.Equal(new ReportMoney(800m, "BDT"), rows[0]["received"]);
        Assert.Equal(new ReportMoney(200m, "BDT"), rows[0]["variance"]);
        Assert.Equal(1L, rows[0]["mismatchesOpen"]);

        // The transfer is matched to the statement.
        Assert.Equal(new ReportMoney(0m, "BDT"), rows[1]["variance"]);
        Assert.Equal(1L, rows[1]["matched"]);

        // The till: counted 290 against 300 expected.
        Assert.Equal(new ReportMoney(290m, "BDT"), rows[2]["received"]);
        Assert.Equal(new ReportMoney(-10m, "BDT"), rows[2]["variance"]);
        Assert.Equal(1L, rows[2]["mismatchesOpen"]);

        // Card in store: expected only, with the note.
        Assert.Null(rows[3]["received"]);
        Assert.Null(rows[3]["variance"]);
        Assert.Equal(PaymentReconciliationBuilder.CardNote, Assert.Single(notes).Text);
    }

    [Fact]
    public void U_D5_APendingOrRejectedPayment_IsNotReconciled()
    {
        var pending = Payment(PaymentMethod.BankTransfer, 500m);
        pending.Status = PaymentStatus.PendingApproval.Value;

        var (rows, _) = PaymentReconciliationBuilder.Build([pending], [], BranchNames, "BDT");

        Assert.Empty(rows);
    }

    [Fact]
    public void TheCashUp_ADayWithNoRecord_IsNotClosed_AndItsFiguresAreBlank()
    {
        var closed = new RvCashUpRow { BranchId = Branch, BusinessDate = October, Status = CashUpStatus.Closed.Value, CashRecorded = 300m, ExpectedCash = 300m, CountedCash = 300m, Variance = 0m, Explanation = "ok" };

        var rows = CashUpReportBuilder.Build([closed], BranchNames, new Dictionary<Guid, string>(), October.AddDays(-1), October.AddDays(1), October, Branch, "BDT");

        Assert.Equal(2, rows.Count); // the day before (not closed) and the day itself; tomorrow is in the future
        Assert.Equal(CashUpReportBuilder.NotClosed, rows[0]["status"]);
        Assert.Null(rows[0]["expected"]);
        Assert.Null(rows[0]["variance"]);
        Assert.Equal("Closed", rows[1]["status"]);
        Assert.Equal(new ReportMoney(0m, "BDT"), rows[1]["variance"]);
    }

    // ── U-D6: ageing boundaries ────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(0, "0-30")]
    [InlineData(30, "0-30")]
    [InlineData(31, "31-60")]
    [InlineData(60, "31-60")]
    [InlineData(61, "61-90")]
    [InlineData(90, "61-90")]
    [InlineData(91, "over90")]
    public void U_D6_TheAgeBoundaries_AreThirtyOneSixtyOneAndNinetyOne(int days, string bucket) => Assert.Equal(bucket, ReceivablesAgeingBuilder.BucketOf(days));

    [Fact]
    public void U_D6_TheAgeingRows_AreGroupedByCustomer_AgedToTheEndOfTheRange_AndShowAMergedCustomerUnderTheSurvivor()
    {
        var survivor = Guid.NewGuid();
        var duplicate = Guid.NewGuid();
        RvInvoiceBalanceRow Invoice(Guid customer, int ageDays, decimal outstanding) => new()
        {
            InvoiceId = Guid.NewGuid(), CustomerId = customer, IssueDate = October.AddDays(-ageDays), OutstandingAmount = outstanding, CurrencyCode = "BDT", BranchId = Branch,
        };

        var rows = ReceivablesAgeingBuilder.Build(
            [Invoice(survivor, 10, 100m), Invoice(duplicate, 45, 200m), Invoice(survivor, 120, 50m), Invoice(survivor, 5, 0m)],
            October, id => (survivor, "Survivor Owner"));

        var row = Assert.Single(rows);
        Assert.Equal("Survivor Owner", row["customerName"]);
        Assert.Equal(3L, row["invoicesOutstanding"]);
        Assert.Equal(new ReportMoney(350m, "BDT"), row["balance"]);
        Assert.Equal(new ReportMoney(100m, "BDT"), row["age0To30"]);
        Assert.Equal(new ReportMoney(200m, "BDT"), row["age31To60"]);
        Assert.Equal(new ReportMoney(0m, "BDT"), row["age61To90"]);
        Assert.Equal(new ReportMoney(50m, "BDT"), row["ageOver90"]);
    }

    // ── U-D7: two currencies are two groups, never added ─────────────────────────────────────────────────────
    [Fact]
    public void U_D7_TwoCurrenciesInOnePeriod_AreTwoGroups_TheTotalsAreOmitted_AndCoverageHasNoHeadline()
    {
        var facts = new[] { Invoice(October, "Shop", 100m, currency: "BDT"), Invoice(October, "Shop", 5m, currency: "USD") };

        var aggregates = RevenueBuilder.Aggregate(facts, TimeBucket.Month, f => [f.Line]);

        Assert.Equal(2, aggregates.Count);
        Assert.Equal(["BDT", "USD"], aggregates.Select(a => a.Currency));
        Assert.Null(RevenueBuilder.Coverage(aggregates)); // never added
        Assert.Null(RevenueBuilder.SumIfOneCurrency(aggregates.Select(a => RevenueBuilder.Money(a.Net, a.Currency))));
        Assert.Equal(new ReportMoney(100m, "BDT"), RevenueBuilder.SumIfOneCurrency([new ReportMoney(40m, "BDT"), new ReportMoney(60m, "BDT")]));
    }

    // ── Dimensions ─────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void AShopLineWithNoSku_IsDeliveryAndFees_WithNoOrigin_AndAProductGroupsByItsCategoryAndOrigin()
    {
        var food = new RvSkuRow { SkuId = Guid.NewGuid(), Category = ProductCategory.CatFood.Value, SubCategory = null, CountryOfOrigin = "FR" };
        var litter = new RvSkuRow { SkuId = Guid.NewGuid(), Category = ProductCategory.Litter.Value, CountryOfOrigin = null };

        var delivery = RevenueFacts.Dimensions(BusinessLine.Shop.Value, ItemKind.Fee.Value, null, null);
        var foodDims = RevenueFacts.Dimensions(BusinessLine.Shop.Value, ItemKind.Product.Value, null, food);
        var litterDims = RevenueFacts.Dimensions(BusinessLine.Shop.Value, ItemKind.Product.Value, null, litter);
        var clinic = RevenueFacts.Dimensions(BusinessLine.Clinic.Value, ItemKind.Service.Value, ServiceCategory.Vaccination.Value, null);

        Assert.Equal("Delivery and fees", delivery.Category);
        Assert.Equal("Not applicable", delivery.Origin);
        Assert.Equal("CatFood", foodDims.Category);
        Assert.Equal("FR", foodDims.Origin);
        Assert.Equal("Not applicable", litterDims.Origin);
        Assert.Equal("Vaccination", clinic.ServiceGroup);
        Assert.Equal("Clinic", clinic.Line);
    }

    [Fact]
    public void TheRefundsReport_GroupsByReasonAndSettlement_AndOnlyKnowsCashInStoreAndBankTransferRefunds()
    {
        var returnNote = new RvCreditNoteRow { CreditNoteId = Guid.NewGuid(), CreditNoteKind = CreditNoteKind.Return.Value, Total = 100m, CurrencyCode = "BDT" };
        var goodwill = new RvCreditNoteRow { CreditNoteId = Guid.NewGuid(), CreditNoteKind = CreditNoteKind.Goodwill.Value, Total = 50m, ReleasedToCustomerCredit = 30m, CurrencyCode = "BDT" };
        var refund = new RvRefundRow { CreditNoteId = returnNote.CreditNoteId, Method = RefundMethod.BankTransfer.Value, Amount = 100m, CurrencyCode = "BDT" };

        var rows = RefundsReportBuilder.Build([returnNote, goodwill], [refund]);

        Assert.Equal(["Goodwill", "Goodwill", "Return"], rows.Select(r => (string)r["reason"]!));
        Assert.Contains(rows, r => (string)r["reason"]! == "Return" && (string)r["refundMethod"]! == "BankTransfer" && (ReportMoney)r["amount"]! == new ReportMoney(100m, "BDT"));
        Assert.Contains(rows, r => (string)r["refundMethod"]! == RefundsReportBuilder.CustomerCredit && (ReportMoney)r["amount"]! == new ReportMoney(30m, "BDT"));
        Assert.Contains(rows, r => (string)r["refundMethod"]! == RefundsReportBuilder.AppliedToBalance && (ReportMoney)r["amount"]! == new ReportMoney(20m, "BDT"));
    }

    [Fact]
    public void TheTaxSummary_HasInvoiceLinesPositive_AndCreditNotesAsNegativeLines_PerClassAndRate()
    {
        RvInvoiceLineRow Line(string code, decimal rate, decimal net, decimal tax) => new() { TaxClassCode = code, TaxRatePercent = rate, NetAmount = net, TaxAmount = tax, CurrencyCode = "BDT" };
        RvCreditNoteLineRow CreditLine(string code, decimal rate, decimal net, decimal tax) => new() { TaxClassCode = code, TaxRatePercent = rate, NetAmount = net, TaxAmount = tax, CurrencyCode = "BDT" };

        var rows = TaxSummaryBuilder.Build(
            [(October, Line("STD", 15m, 1000m, 150m)), (October, Line("STD", 15m, 200m, 30m)), (October, Line("ZERO", 0m, 400m, 0m))],
            [(October, CreditLine("STD", 15m, 100m, 15m))], TimeBucket.Month);

        var invoices = rows.Where(r => (string)r["source"]! == "Invoices").ToList();
        Assert.Equal(180m, invoices.Sum(r => ((ReportMoney)r["outputTax"]!).Amount)); // = the invoices' tax (I-D6)
        var credit = Assert.Single(rows, r => (string)r["source"]! == "Credit notes");
        Assert.Equal(new ReportMoney(-15m, "BDT"), credit["outputTax"]);
        Assert.Equal(new ReportMoney(-100m, "BDT"), credit["taxableNet"]);
    }
}
