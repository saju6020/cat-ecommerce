using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-BIL-001 §15 -- the pure rules: financial year, number format, status derivation, the
/// payment-method flags and the C-5 card-number guard (U-C1…U-C4, U-D1, U-D3).</summary>
public sealed class BillingRulesTests
{
    // ── U-C1 ────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("2026-06-30", 7, "2025-26")]
    [InlineData("2026-07-01", 7, "2026-27")]
    [InlineData("2027-06-30", 7, "2026-27")]
    [InlineData("2026-12-31", 1, "2026")]
    [InlineData("2026-01-01", 1, "2026")]
    [InlineData("2026-03-31", 4, "2025-26")]
    [InlineData("2099-09-01", 7, "2099-00")]
    public void U_C1_FinancialYear(string date, int startMonth, string expected)
    {
        Assert.Equal(expected, FinancialYearRules.For(DateOnly.Parse(date), startMonth));
    }

    [Fact]
    public void FinancialYear_RejectsAnInvalidStartMonth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FinancialYearRules.For(new DateOnly(2026, 1, 1), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FinancialYearRules.For(new DateOnly(2026, 1, 1), 13));
    }

    // ── U-C2 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_C2_DocumentNumberFormat()
    {
        Assert.Equal("INV-MAIN-2627-000123", DocumentNumberFormat.Format(BillingDocumentKind.Invoice, "MAIN", "2026-27", 123));
        Assert.Equal("CN-MAIN-2627-000007", DocumentNumberFormat.Format(BillingDocumentKind.CreditNote, "MAIN", "2026-27", 7));
        Assert.Equal("INV-MAIN-2026-000001", DocumentNumberFormat.Format(BillingDocumentKind.Invoice, "MAIN", "2026", 1));
    }

    // ── U-C3 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_C3_SeriesTake_ReturnsOneTwoThree_AndAdvances()
    {
        var series = new DocumentNumberSeriesAggregate(Guid.NewGuid());
        series.Open(Guid.NewGuid(), "2026-27", BillingDocumentKind.Invoice, Tests.TestSupport.TestUserContext.Create());

        Assert.Equal([1L, 2L, 3L], [series.Take(), series.Take(), series.Take()]);
        Assert.Equal(4, series.NextSequence);
    }

    // ── U-C4 ────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(1000, 0, 0, "Issued")]
    [InlineData(1000, 400, 0, "PartPaid")]
    [InlineData(1000, 1000, 0, "Paid")]
    [InlineData(1000, 0, 300, "Issued")]
    [InlineData(1000, 700, 300, "Paid")]
    [InlineData(1000, 400, 300, "PartPaid")]
    [InlineData(1000, 0, 1000, "Credited")]
    [InlineData(1000, 0, 0.01, "Issued")]
    public void U_C4_StatusIsDerivedFromTheTotals(double total, double paid, double credited, string expected)
    {
        Assert.Equal(expected, InvoiceStatusRules.Derive((decimal)total, (decimal)paid, (decimal)credited).DisplayName);
    }

    [Fact]
    public void U_C4b_OutstandingIsTotalMinusCreditedMinusPaid()
    {
        Assert.Equal(300m, InvoiceStatusRules.Outstanding(1000m, 400m, 300m));
    }

    [Fact]
    public void ActionRules_OnlyADraftMayChangeContent()
    {
        Assert.True(InvoiceActionRules.CanChangeContent(InvoiceStatus.Draft));
        foreach (var status in new[] { InvoiceStatus.Issued, InvoiceStatus.PartPaid, InvoiceStatus.Paid, InvoiceStatus.Credited, InvoiceStatus.Discarded })
            Assert.False(InvoiceActionRules.CanChangeContent(status));
    }

    // ── U-D1 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D1_EveryPaymentMethod_HasEveryFlag_AndNoneIsOnline()
    {
        var methods = Enumeration.GetAll<PaymentMethod>().ToList();
        Assert.Equal(4, methods.Count);

        foreach (var method in methods)
        {
            Assert.False(method.IsOnline, $"{method.DisplayName} must not be online before the gateway exists (ADR-182).");
            Assert.False(method.SupportsAutomatedRefund);
            Assert.NotEmpty(method.AllowedRefundMethods);
            Assert.False(string.IsNullOrWhiteSpace(method.ReferenceLabel));
        }
    }

    [Fact]
    public void U_D1b_TheFlagsMatchThePaymentMethodTable()
    {
        Assert.True(PaymentMethod.CashInStore.CountsInTill);
        Assert.False(PaymentMethod.CashInStore.RequiresReference);
        Assert.True(PaymentMethod.CardInStore.RequiresReference);
        Assert.False(PaymentMethod.CardInStore.CountsInTill);
        Assert.True(PaymentMethod.BankTransfer.RequiresReference);
        Assert.True(PaymentMethod.CashOnDelivery.RequiresCollector);
        Assert.False(PaymentMethod.CashOnDelivery.RequiresReference);

        Assert.True(PaymentMethodRules.CountsInTill(PaymentMethod.CashOnDelivery, CollectorKind.Staff));
        Assert.False(PaymentMethodRules.CountsInTill(PaymentMethod.CashOnDelivery, CollectorKind.Courier));
        Assert.Equal([RefundMethod.CashInStore], PaymentMethod.CardInStore.AllowedRefundMethods);
        Assert.Equal([RefundMethod.BankTransfer], PaymentMethod.BankTransfer.AllowedRefundMethods);
        Assert.Equal([RefundMethod.CashInStore, RefundMethod.BankTransfer], PaymentMethod.CashOnDelivery.AllowedRefundMethods);
    }

    // ── U-D3 ────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("4111 1111 1111 1111", true)]
    [InlineData("4111-1111-1111-1111", true)]
    [InlineData("pay with 4111111111111111 please", true)]
    [InlineData("ref 9 4111 1111 1111 1111", true)]    // a stray number right before the card must not hide it
    [InlineData("77-4111-1111-1111-1111 paid", true)]
    [InlineData("TRX 20260101 0012", false)]
    [InlineData("4111-1111-1111-1112", false)]       // Luhn fails
    [InlineData("004512", false)]                     // a terminal slip number
    [InlineData("12345678901234567890", false)]       // 20 digits
    [InlineData("TRX-2026-000123", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void U_D3_CardNumberGuard(string? text, bool expected)
    {
        Assert.Equal(expected, CardNumberGuard.LooksLikeCardNumber(text));
    }

    // ── C-5 probes: the separators and digits a person (or a copy-paste) can use ─────────────────────
    [Theory]
    [InlineData("4111  1111  1111  1111")]                       // a double space
    [InlineData("4111\t1111\t1111\t1111")]                       // tabs
    [InlineData("4111 1111 1111 1111")]            // non-breaking spaces
    [InlineData("4111 - 1111 - 1111 - 1111")]                    // spaced dashes
    [InlineData("4111.1111.1111.1111")]
    [InlineData("4111/1111/1111/1111")]
    [InlineData("4111_1111_1111_1111")]
    [InlineData("৪১১১ ১১১১ ১১১১ ১১১১")] // Bengali digits
    [InlineData("４１１１１１１１１１１１１１１１")]       // full-width digits
    [InlineData("94111111111111111")]                            // a stray digit glued to the front
    [InlineData("card=4111 1111 1111 1111;")]
    public void C5_EveryWayOfWritingACardNumber_IsRefused_AndMasked(string text)
    {
        Assert.True(CardNumberGuard.LooksLikeCardNumber(text));

        var masked = CardNumberGuard.MaskCardNumbers(text);
        Assert.Contains("••••1111", masked);
        Assert.False(CardNumberGuard.LooksLikeCardNumber(masked), "A masked value must not still look like a card number.");
        Assert.DoesNotContain("4111", masked);
        Assert.DoesNotContain("৪১১১", masked);
    }

    [Theory]
    [InlineData("Invoice 2026-10-07 paid 1500.00")]
    [InlineData("TRX 20260101 0012")]
    [InlineData("call 01712 345678 after 5.30")]
    [InlineData("order 1234 of 56 on 7/10/2026")]
    [InlineData("১২৩৪")]                      // short Bengali number
    public void C5_OrdinaryNumbers_AreNotRefused_AndNotMasked(string text)
    {
        Assert.False(CardNumberGuard.LooksLikeCardNumber(text));
        Assert.Equal(text, CardNumberGuard.MaskCardNumbers(text));
    }

    // ── U-G5 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_G5_ACardLookingDigitRun_IsMaskedToItsLastFour()
    {
        Assert.Equal("Payment from ••••1111 ref 77", CardNumberGuard.MaskCardNumbers("Payment from 4111 1111 1111 1111 ref 77"));
        Assert.Equal("Order 20260101 paid", CardNumberGuard.MaskCardNumbers("Order 20260101 paid"));
        Assert.Equal("ref 9 ••••1111 end", CardNumberGuard.MaskCardNumbers("ref 9 4111 1111 1111 1111 end"));
        Assert.Equal("a ••••1111 b ••••1111", CardNumberGuard.MaskCardNumbers("a 4111 1111 1111 1111 b 4111111111111111"));
    }

    [Fact]
    public void Bounds_MatchTheHandover()
    {
        Assert.Equal(500, BillingRules.MaxInvoiceLines);
        Assert.Equal(5, BillingRules.MaxNumberingAttempts);
        Assert.Equal(31, BillingRules.MaxBackdatedReceiptDays);
        Assert.Equal(200, BillingRules.MaxAllocationEntries);
        Assert.Equal(5000, BillingRules.MaxStatementLines);
        Assert.Equal(1_048_576, BillingRules.MaxStatementBytes);
        Assert.Equal(366, BillingRules.MaxExportRangeDays);
        Assert.Equal(200_000, BillingRules.MaxExportRows);
    }

    [Fact]
    public void Money_MessagesFormatInTheCurrency()
    {
        Assert.Equal("Only BDT 400.00 is outstanding on INV-MAIN-2627-000123.",
            BillingMessages.AllocationExceedsOutstanding(400m, "BDT", "INV-MAIN-2627-000123"));
        Assert.Equal("BDT 1,500.00", BillingMessages.FormatMoney(1500m, "BDT"));
    }
}
