using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Ordering;

/// <summary>HO-ORD-001 §15 group A -- money in integer minor units (US-ORD-04 AC2, ADR-198).</summary>
public sealed class MinorUnitsTests
{
    // ── U-A1 ────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("0.01", 1L)]
    [InlineData("1250.50", 125_050L)]
    [InlineData("9999999999.99", 999_999_999_999L)]
    public void U_A1_MinorUnits_RoundTripsWithMoney_Exactly(string text, long expectedMinor)
    {
        var amount = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        var minor = MinorUnits.FromAmount(amount);
        Assert.Equal(expectedMinor, minor.Value);
        Assert.Equal(amount, minor.ToAmount());

        var money = new Money(amount, "BDT");
        Assert.Equal(expectedMinor, money.MinorUnits);
        Assert.Equal(money, Money.FromMinorUnits(minor.Value, "BDT"));
    }

    [Fact]
    public void U_A1_AnAmountWithMoreThanTwoDecimals_IsRefused_NotSilentlyRounded()
    {
        Assert.Throws<ArgumentException>(() => MinorUnits.FromAmount(1.005m));
        Assert.Throws<ArgumentException>(() => MinorUnits.PercentToHundredths(15.005m));
    }

    // ── U-A2 ────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(5L, 2L, 3L)]     // 2.5 -> 3
    [InlineData(-5L, 2L, -3L)]   // -2.5 -> -3
    [InlineData(7L, 2L, 4L)]     // 3.5 -> 4
    [InlineData(3L, 2L, 2L)]     // 1.5 -> 2
    [InlineData(1L, 3L, 0L)]     // 0.33 -> 0
    [InlineData(2L, 3L, 1L)]     // 0.67 -> 1
    [InlineData(-1L, 3L, 0L)]
    [InlineData(-2L, 3L, -1L)]
    public void U_A2_Rounding_IsHalfAwayFromZero(long numerator, long denominator, long expected) =>
        Assert.Equal(expected, MinorUnits.DivideRoundHalfAwayFromZero(numerator, denominator));

    [Fact]
    public void U_A2_APercentage_IsRoundedAtTheLine_NeverOnTheTotal()
    {
        // 15 % of 0.10 = 0.015 -> 0.02 (half away from zero); two such lines are 0.04, but 15 % of their 0.20 total is 0.03.
        var perLine = new MinorUnits(10).Percent(MinorUnits.PercentToHundredths(15m));
        Assert.Equal(2L, perLine.Value);

        var onTotal = new MinorUnits(20).Percent(MinorUnits.PercentToHundredths(15m));
        Assert.Equal(3L, onTotal.Value);
        Assert.NotEqual(perLine.Value * 2, onTotal.Value);
    }

    // ── U-A3 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_A3_100OverThreeEqualLines_Is34_33_33()
    {
        var shares = MinorUnits.Apportion(new MinorUnits(100), [new(1), new(1), new(1)]);
        Assert.Equal([34L, 33L, 33L], shares.Select(s => s.Value));
    }

    [Fact]
    public void U_A3_1OverThreeLines_Is1_0_0()
    {
        var shares = MinorUnits.Apportion(new MinorUnits(1), [new(1), new(1), new(1)]);
        Assert.Equal([1L, 0L, 0L], shares.Select(s => s.Value));
    }

    [Theory]
    [InlineData(18_200L, new long[] { 170_000, 17_000, 12_000 })]
    [InlineData(10_000L, new long[] { 1, 2, 3, 4, 5, 6, 7 })]
    [InlineData(99L, new long[] { 3, 3, 3, 1 })]
    [InlineData(0L, new long[] { 5, 5 })]
    public void U_A3_ApportionedSharesAlwaysSumExactly(long total, long[] weights)
    {
        var shares = MinorUnits.Apportion(new MinorUnits(total), weights.Select(w => new MinorUnits(w)).ToList());

        Assert.Equal(total, shares.Sum(s => s.Value));
        Assert.All(shares, s => Assert.True(s.Value >= 0));
    }

    [Fact]
    public void Apportion_WithAllZeroWeights_GivesTheWholeTotalToTheFirst()
    {
        var shares = MinorUnits.Apportion(new MinorUnits(7), [new(0), new(0)]);
        Assert.Equal([7L, 0L], shares.Select(s => s.Value));
    }

    [Fact]
    public void Apportion_DoesNotOverflow_AtTheLargestMoneyValue()
    {
        var max = MinorUnits.FromAmount(9_999_999_999.99m);
        var shares = MinorUnits.Apportion(max, [max, max, max]);
        Assert.Equal(max.Value, shares.Sum(s => s.Value));
    }

    // ── U-A4 ────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("BDT", true)]
    [InlineData("usd", true)]
    [InlineData("KWD", false)]   // 3 minor digits
    [InlineData("JPY", false)]   // 0 minor digits
    [InlineData("ZZZ", false)]   // unknown
    public void U_A4_OnlyATwoDigitCurrency_IsSupported(string code, bool supported) =>
        Assert.Equal(supported, CurrencyMinorDigits.IsSupported(code));
}
