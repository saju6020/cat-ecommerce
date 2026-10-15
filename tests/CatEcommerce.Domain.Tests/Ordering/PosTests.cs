using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Ordering;

/// <summary>HO-ORD-001 Phase H units U-H1 (the tender rule) and U-H3 (the 48-column receipt). U-H2 (three history rows) is in <c>OrderAggregateTests</c>.</summary>
public sealed class PosTests
{
    private static MinorUnits M(decimal amount) => MinorUnits.FromAmount(amount);

    private static CounterTender Cash(decimal amount) => new(TenderMethod.Cash, M(amount), null);

    private static CounterTender Card(decimal amount, string? reference = "SLIP-1") => new(TenderMethod.InStoreCard, M(amount), reference);

    // U-H1
    [Fact]
    public void CashHandedOver_GivesChange_AndIsRecordedAtTheAmountDue()
    {
        var outcome = TenderCalculator.Evaluate(M(1927.50m), [Cash(2000m)]);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(M(72.50m), outcome.ChangeDue);
        var payment = Assert.Single(outcome.Payments);
        Assert.Equal(TenderMethod.Cash, payment.Method);
        Assert.Equal(M(1927.50m), payment.Amount);
    }

    [Fact]
    public void CardPlusCash_TheCashCoversTheRest_AndTheChangeIsTheOverpayment()
    {
        var outcome = TenderCalculator.Evaluate(M(1927.50m), [Card(1000m), Cash(1000m)]);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(M(72.50m), outcome.ChangeDue);
        Assert.Equal(M(1927.50m), new MinorUnits(outcome.Payments.Sum(p => p.Amount.Value)));
        Assert.Equal(M(927.50m), outcome.Payments.Single(p => p.Method == TenderMethod.Cash).Amount);
    }

    [Fact]
    public void ACardAboveTheBalance_IsRefused_ThereIsNoCashBackOnACard()
    {
        var outcome = TenderCalculator.Evaluate(M(1927.50m), [Card(2000m)]);

        Assert.Equal(BusinessViolationCodes.TendersDoNotCoverTotal, outcome.ViolationCode);
        Assert.Empty(outcome.Payments);
    }

    [Theory]
    [InlineData(1927.49)]
    [InlineData(0.01)]
    public void CashThatFallsShort_IsRefused(double handed) =>
        Assert.Equal(BusinessViolationCodes.TendersDoNotCoverTotal, TenderCalculator.Evaluate(M(1927.50m), [Cash((decimal)handed)]).ViolationCode);

    [Fact]
    public void ExactCash_NeedsNoChange_AndAnExactCard_NeedsNoCash()
    {
        Assert.Equal(MinorUnits.Zero, TenderCalculator.Evaluate(M(100m), [Cash(100m)]).ChangeDue);

        var card = TenderCalculator.Evaluate(M(100m), [Card(100m)]);
        Assert.True(card.IsSuccess);
        Assert.Single(card.Payments);
        Assert.Equal(TenderMethod.InStoreCard, card.Payments[0].Method);
    }

    [Fact]
    public void ACardNeedsItsSlipReference_AndNoTenderMayBeZeroOrMissing()
    {
        Assert.Equal(BusinessViolationCodes.TerminalReferenceRequired, TenderCalculator.Evaluate(M(100m), [Card(100m, null)]).ViolationCode);
        Assert.Equal(BusinessViolationCodes.TerminalReferenceRequired, TenderCalculator.Evaluate(M(100m), [Card(100m, " ")]).ViolationCode);
        Assert.Equal(BusinessViolationCodes.TendersDoNotCoverTotal, TenderCalculator.Evaluate(M(100m), []).ViolationCode);
        Assert.Equal(BusinessViolationCodes.TendersDoNotCoverTotal, TenderCalculator.Evaluate(M(100m), [Cash(0m), Cash(100m)]).ViolationCode);
    }

    // U-H3
    private static ReceiptText Receipt(string description = "Royal Canin Kitten Dry Food Persian Sterilised 2 kg Value Pack", string branch = "PurrCare Gulshan Branch") => new(
        branch, ["House 12, Road 5, Gulshan Avenue, Dhaka 1212, Bangladesh"], ["PurrCare Test Ltd", "Tax reg: BIN-0000012345-0101"], "INV-2026-000123", "ORD-20261009-0042", "2026-10-09 14:05",
        "Nusrat Jahan Rahman-Chowdhury", "BDT",
        [new ReceiptTextLine(description, 12, "12,850.00", "154,200.00", "1,000.00", "Tax 15%: 23,130.00"), new ReceiptTextLine("Feather wand", 1, "120.00", "120.00", null, "Tax 0%: 0.00")],
        [("Subtotal", "154,320.00"), ("Discount", "-1,000.00"), ("Tax", "23,130.00"), ("TOTAL BDT", "176,450.00")],
        [("CardInStore", "100,000.00"), ("CashInStore", "76,450.00")], "3,550.00", "Thank you for choosing PurrCare. Please keep this receipt for returns within seven days.");

    [Fact]
    public void EveryLineOfTheReceipt_IsAtMost48Columns_EvenForLongNamesAndAmounts()
    {
        var text = ReceiptTextRenderer.Render(Receipt());

        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        Assert.All(lines, l => Assert.True(l.Length <= ReceiptTextRenderer.Columns, $"'{l}' is {l.Length} columns"));
        Assert.Contains("Royal Canin Kitten Dry Food Persian", text);
    }

    [Fact]
    public void TheTotals_AreRightAligned_AndNoAmountIsBrokenAcrossLines()
    {
        var text = ReceiptTextRenderer.Render(Receipt());
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        var total = Assert.Single(lines, l => l.StartsWith("TOTAL BDT"));
        Assert.Equal(48, total.Length);
        Assert.EndsWith("176,450.00", total);
        Assert.Contains(lines, l => l.StartsWith("Change") && l.EndsWith("3,550.00") && l.Length == 48);
        Assert.Contains(lines, l => l.EndsWith("154,200.00"));       // the line total sits whole on the right edge
        foreach (var amount in new[] { "12,850.00", "154,200.00", "1,000.00", "23,130.00", "154,320.00", "176,450.00", "100,000.00", "76,450.00", "3,550.00" })
            Assert.Contains(amount, text);
    }

    [Fact]
    public void ALongUnbrokenWord_IsSplitRatherThanOverflowing()
    {
        var text = ReceiptTextRenderer.Render(Receipt(description: new string('X', 130), branch: new string('B', 60)));

        Assert.All(text.Split('\n').Select(l => l.TrimEnd('\r')), l => Assert.True(l.Length <= 48));
    }
}
