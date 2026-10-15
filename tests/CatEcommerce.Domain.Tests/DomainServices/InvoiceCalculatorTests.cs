using System.Reflection;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-BIL-001 §15 group A (U-A1…U-A8) -- tax, rounding and money without floats (ADR-181, ADR-194).</summary>
public sealed class InvoiceCalculatorTests
{
    private static CalcLineInput Line(decimal quantity, decimal unitPrice, decimal discount, decimal rate, bool qualifies = true, Guid? key = null, string currency = "BDT") =>
        new(key ?? Guid.NewGuid(), currency, quantity, unitPrice, discount, rate, qualifies);

    [Fact]
    public void U_A1_LineWithDiscount_NetTaxGross()
    {
        var totals = InvoiceCalculator.Calculate([Line(2m, 450m, 50m, 15m)]);

        var line = Assert.Single(totals.Lines);
        Assert.Equal(850.00m, line.NetAmount);
        Assert.Equal(127.50m, line.TaxAmount);
        Assert.Equal(977.50m, line.GrossAmount);
        Assert.Equal(977.50m, totals.Total);
    }

    [Theory]
    [InlineData(33.33, 7.5, 2.50)]   // 2.49975 -> 2.50
    [InlineData(0.10, 5, 0.01)]      // 0.005 rounds up in magnitude (away from zero), not to even
    [InlineData(0.50, 5, 0.03)]      // 0.025 -> 0.03 (banker rounding would give 0.02)
    [InlineData(1.50, 5, 0.08)]      // 0.075 -> 0.08
    public void U_A2_TaxRoundsHalfAwayFromZero(double net, double rate, double expectedTax)
    {
        Assert.Equal((decimal)expectedTax, InvoiceCalculator.TaxOf((decimal)net, (decimal)rate));
    }

    [Fact]
    public void U_A3_InvoiceTaxIsTheSumOfLineTaxes_NeverTheTaxOfTheSum()
    {
        // Three lines of 0.10 at 5 %: each line tax is 0.005 -> 0.01, so the invoice tax is 0.03;
        // the tax of the sum (0.30 at 5 %) would have been 0.02.
        var totals = InvoiceCalculator.Calculate([Line(1m, 0.10m, 0m, 5m), Line(1m, 0.10m, 0m, 5m), Line(1m, 0.10m, 0m, 5m)]);
        Assert.Equal(0.03m, totals.TaxTotal);
        Assert.Equal(0.02m, InvoiceCalculator.TaxOf(0.30m, 5m));

        var mixed = InvoiceCalculator.Calculate([Line(1m, 1000m, 0m, 15m), Line(1m, 1000m, 0m, 5m), Line(1m, 1000m, 0m, 0m)]);
        Assert.Equal([150m, 50m, 0m], mixed.Lines.Select(l => l.TaxAmount).ToArray());
        Assert.Equal(200m, mixed.TaxTotal);
        Assert.Equal(3200m, mixed.Total);
    }

    [Fact]
    public void U_A4_InvoiceLevelDiscount_IsApportionedLargestRemainder_FeeLineUntouched()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var fee = Guid.NewGuid();

        var totals = InvoiceCalculator.Calculate(
            [Line(1m, 300m, 0m, 10m, key: a), Line(1m, 200m, 0m, 10m, key: b), Line(1m, 100m, 0m, 10m, key: c), Line(1m, 50m, 0m, 10m, qualifies: false, key: fee)],
            invoiceDiscount: 100m);

        var shares = totals.Lines.ToDictionary(l => l.Key, l => l.InvoiceDiscountShare);
        Assert.Equal(50.00m, shares[a]);
        Assert.Equal(33.33m, shares[b]);
        Assert.Equal(16.67m, shares[c]);
        Assert.Equal(0m, shares[fee]);
        Assert.Equal(100m, shares.Values.Sum());

        // Tax is on the reduced nets.
        var lineA = totals.Lines.Single(l => l.Key == a);
        Assert.Equal(250m, lineA.NetAmount);
        Assert.Equal(25m, lineA.TaxAmount);
        Assert.Equal(50m, totals.Lines.Single(l => l.Key == fee).NetAmount);
    }

    [Fact]
    public void U_A4b_ApportionedSharesAlwaysSumToTheDiscount()
    {
        var keys = Enumerable.Range(0, 7).Select(_ => Guid.NewGuid()).ToList();
        var shares = InvoiceCalculator.Apportion(0.01m, keys.Select(k => (k, 100m)).ToList());
        Assert.Equal(0.01m, shares.Values.Sum());

        var uneven = InvoiceCalculator.Apportion(100m, [(keys[0], 1m), (keys[1], 1m), (keys[2], 1m)]);
        Assert.Equal(100m, uneven.Values.Sum());
    }

    [Fact]
    public void U_A5_DiscountGreaterThanQuantityTimesPrice_IsRefused()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => InvoiceCalculator.Calculate([Line(2m, 100m, 200.01m, 0m)]));
        Assert.Equal(BusinessViolationCodes.DiscountExceedsAmount, ex.ErrorCode);
    }

    [Fact]
    public void U_A6_MixedCurrencies_AreRefused()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => InvoiceCalculator.Calculate([Line(1m, 1m, 0m, 0m), Line(1m, 1m, 0m, 0m, currency: "USD")]));
        Assert.Equal(BusinessViolationCodes.CurrencyMismatch, ex.ErrorCode);
    }

    [Fact]
    public void U_A7_NoFloatingPointAnywhereInTheMoneyCode()
    {
        var assembly = typeof(InvoiceCalculator).Assembly;
        var types = new List<Type> { typeof(InvoiceCalculator), typeof(CashUpCalculator), typeof(AgeingCalculator), typeof(BankMatchSuggester) };
        types.AddRange(assembly.GetTypes().Where(t => t.Namespace == typeof(InvoiceAggregate).Namespace && !t.IsAbstract));
        types.AddRange(assembly.GetTypes().Where(t => t.Namespace == "CatEcommerce.Domain.Entities" && t.Name is "InvoiceLine" or "AllocationEntry" or "CreditNoteLine" or "RemittanceLine" or "BankStatementLine"));

        var offenders = new List<string>();
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var type in types.Distinct())
        {
            offenders.AddRange(type.GetFields(all).Where(f => IsFloat(f.FieldType)).Select(f => $"{type.Name}.{f.Name}"));
            offenders.AddRange(type.GetProperties(all).Where(p => IsFloat(p.PropertyType)).Select(p => $"{type.Name}.{p.Name}"));
            foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
            {
                if (method.GetParameters().Any(p => IsFloat(p.ParameterType))) offenders.Add($"{type.Name}.{method.Name} parameter");
                if (method is MethodInfo mi && IsFloat(mi.ReturnType)) offenders.Add($"{type.Name}.{method.Name} return");
                var body = method.GetMethodBody();
                if (body is not null && body.LocalVariables.Any(l => IsFloat(l.LocalType))) offenders.Add($"{type.Name}.{method.Name} local");
            }
        }

        Assert.True(offenders.Count == 0, "Floating point found: " + string.Join(", ", offenders));
    }

    private static bool IsFloat(Type t) => t == typeof(double) || t == typeof(float) || t == typeof(double?) || t == typeof(float?);

    [Fact]
    public void U_A8_CreditOfOneOfThreeUnits_UsesTheFrozenRate_NotTheCurrentOne()
    {
        // 3 x 100 at a frozen 15 %: net 300, tax 45, gross 345.
        var basis = new CreditLineBasis(Guid.NewGuid(), "Item", 3m, 300m, 45m, 345m, 15m, 0m, 0m, 0m, 0m);

        var credit = InvoiceCalculator.CreditByQuantity(basis, 1m);

        Assert.Equal(100m, credit.Net);
        Assert.Equal(15m, credit.Tax);
        Assert.Equal(115m, credit.Gross);
    }

    [Fact]
    public void CreditOfTheRemainder_TiesOutExactly()
    {
        var basis = new CreditLineBasis(Guid.NewGuid(), "Item", 3m, 100m, 15m, 115m, 15m, 2m, 66.67m, 10m, 76.67m);

        var credit = InvoiceCalculator.CreditByQuantity(basis, 1m);

        Assert.Equal(33.33m, credit.Net);
        Assert.Equal(5m, credit.Tax);
        Assert.Equal(38.33m, credit.Gross);
    }

    [Fact]
    public void CreditAboveTheLineRemaining_IsRefused()
    {
        var basis = new CreditLineBasis(Guid.NewGuid(), "Item", 1m, 100m, 15m, 115m, 15m, 0m, 0m, 0m, 0m);

        Assert.Equal(BusinessViolationCodes.CreditExceedsLine, Assert.Throws<DomainRuleViolationException>(() => InvoiceCalculator.CreditByAmount(basis, 115.01m)).ErrorCode);
        Assert.Equal(BusinessViolationCodes.CreditExceedsLine, Assert.Throws<DomainRuleViolationException>(() => InvoiceCalculator.CreditByQuantity(basis, 2m)).ErrorCode);
    }
}
