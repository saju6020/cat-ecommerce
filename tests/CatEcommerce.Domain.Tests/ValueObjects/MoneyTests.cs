using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Domain.Tests.ValueObjects;

/// <summary>
/// TEST gate seventh pass: U-D7, previously BLOCKED -- no <see cref="Money"/> value-object test
/// file existed anywhere in the tree. "Negative rejected" (the original §15 wording) does NOT hold
/// for this type as actually built: <see cref="Money"/>'s own doc comment is explicit that
/// non-negativity is "each owning aggregate's job, not this type's" (e.g.
/// <c>SetServicePriceCommandValidator.Amount.GreaterThanOrEqualTo(0)</c> is where a negative price
/// is actually refused) -- <see cref="Constructor_NegativeAmount_IsNotRejectedByThisType"/> proves
/// the current, deliberate behaviour instead of asserting a claim the type was never meant to make.
/// </summary>
public sealed class MoneyTests
{
    [Fact]
    public void Constructor_TakesDecimalNotDouble()
    {
        var constructor = typeof(Money).GetConstructors().Single();
        var parameterTypes = constructor.GetParameters().Select(p => p.ParameterType).ToList();

        Assert.Contains(typeof(decimal), parameterTypes);
        Assert.DoesNotContain(typeof(double), parameterTypes);
        Assert.DoesNotContain(typeof(float), parameterTypes);
    }

    [Theory]
    [InlineData(19.999, 20.00)] // rounds up
    [InlineData(2.125, 2.12)] // banker's rounding: 2 (even) wins over 3
    [InlineData(2.135, 2.14)] // banker's rounding: 4 (even) wins over 3
    [InlineData(10, 10.00)]
    public void Constructor_RoundsToTwoDecimalPlaces_BankersRounding(decimal amount, decimal expected)
    {
        var money = new Money(amount, "BDT");

        Assert.Equal(expected, money.Amount);
    }

    [Fact]
    public void Constructor_NegativeAmount_IsNotRejectedByThisType()
    {
        var money = new Money(-5m, "BDT");

        Assert.Equal(-5.00m, money.Amount);
    }

    [Theory]
    [InlineData("bdt", "BDT")]
    [InlineData("Usd", "USD")]
    public void Constructor_CurrencyCode_IsUpperCased(string raw, string expected)
    {
        var money = new Money(1m, raw);

        Assert.Equal(expected, money.CurrencyCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("BD")]
    [InlineData("BDTX")]
    [InlineData(" ")]
    public void Constructor_CurrencyCodeNotThreeLetters_ThrowsArgumentException(string currencyCode)
    {
        Assert.Throws<ArgumentException>(() => new Money(1m, currencyCode));
    }

    [Fact]
    public void MinorUnits_And_FromMinorUnits_RoundTripLosslessly()
    {
        var money = new Money(1500.75m, "BDT");

        Assert.Equal(150075, money.MinorUnits);
        Assert.Equal(money, Money.FromMinorUnits(150075, "BDT"));
    }

    [Fact]
    public void Zero_HasZeroAmount_AndTheGivenCurrency()
    {
        var money = Money.Zero("BDT");

        Assert.Equal(0m, money.Amount);
        Assert.Equal("BDT", money.CurrencyCode);
    }

    [Fact]
    public void Addition_SameCurrency_SumsAmounts()
    {
        var result = new Money(10m, "BDT") + new Money(5.5m, "BDT");

        Assert.Equal(15.50m, result.Amount);
    }

    [Fact]
    public void Addition_DifferentCurrency_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Money(10m, "BDT") + new Money(5m, "USD"));
    }

    [Fact]
    public void Subtraction_SameCurrency_SubtractsAmounts()
    {
        var result = new Money(10m, "BDT") - new Money(4m, "BDT");

        Assert.Equal(6.00m, result.Amount);
    }
}
