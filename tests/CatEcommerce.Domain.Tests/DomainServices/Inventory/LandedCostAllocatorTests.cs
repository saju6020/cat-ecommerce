using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.Tests.DomainServices.Inventory;

/// <summary>HO-IMP-001 §15 phase G -- U-G1 ... U-G4: landed cost is shared by accepted quantity in minor units by largest remainder, so the allocated amounts
/// add up to the costs exactly.</summary>
public sealed class LandedCostAllocatorTests
{
    private static readonly IReadOnlyDictionary<string, decimal> NoRates = new Dictionary<string, decimal>();
    private readonly LandedCostAllocator _allocator = new();

    private static LandedCostBatchInput Batch(decimal quantity, decimal unitCost = 1m, string currency = "BDT") => new(Guid.NewGuid(), quantity, unitCost, currency);

    [Fact]
    public void U_G1_CostsOf1000OverQuantities3_3_3_Are_333_34_333_33_333_33_AndSumExactly()
    {
        var result = _allocator.Allocate([Batch(3m), Batch(3m), Batch(3m)], [new LandedCostComponentInput(1000.00m, "BDT")], NoRates, "BDT");

        Assert.True(result.IsSuccess);
        Assert.Equal([333.34m, 333.33m, 333.33m], result.Lines.Select(l => l.AllocatedCostBase));
        Assert.Equal(1000.00m, result.Lines.Sum(l => l.AllocatedCostBase));
        Assert.Equal(1000.00m, result.TotalCostsBase);
    }

    [Fact]
    public void U_G2_PurchaseInUsdAtTheConsignmentRate_GivesThePurchaseBaseAndFourDecimalLandedUnitCost()
    {
        var batch = Batch(100m, 2.5000m, "USD");

        var result = _allocator.Allocate([batch], [new LandedCostComponentInput(1500.00m, "BDT")], new Dictionary<string, decimal> { ["USD"] = 118.5m }, "BDT");

        var line = Assert.Single(result.Lines);
        Assert.Equal(29625.00m, line.PurchaseCostBase);
        Assert.Equal(1500.00m, line.AllocatedCostBase);
        Assert.Equal(311.2500m, line.LandedUnitCostBase);
    }

    [Fact]
    public void U_G2_ACostInAForeignCurrencyIsConvertedAndRoundedPerComponentBeforeTheShare()
    {
        var result = _allocator.Allocate(
            [Batch(1m), Batch(1m)], [new LandedCostComponentInput(100.50m, "USD"), new LandedCostComponentInput(10.00m, "BDT")],
            new Dictionary<string, decimal> { ["USD"] = 118.5m }, "BDT");

        Assert.Equal(11909.25m + 10.00m, result.TotalCostsBase); // 100.50 x 118.5 = 11 909.25
        Assert.Equal(result.TotalCostsBase, result.Lines.Sum(l => l.AllocatedCostBase));
    }

    [Fact]
    public void U_G2_TheShareFollowsTheAcceptedQuantity_AndTheLandedUnitIsRoundedToFourDecimals()
    {
        var result = _allocator.Allocate([Batch(1m, 2m), Batch(2m, 2m)], [new LandedCostComponentInput(10.00m, "BDT")], NoRates, "BDT");

        Assert.Equal([3.33m, 6.67m], result.Lines.Select(l => l.AllocatedCostBase));
        Assert.Equal(5.3300m, result.Lines[0].LandedUnitCostBase); // (2.00 + 3.33) / 1
        Assert.Equal(5.3350m, result.Lines[1].LandedUnitCostBase); // (4.00 + 6.67) / 2 = 5.335
    }

    [Fact]
    public void U_G3_ACostOrPurchaseInACurrencyWithoutARate_NamesThatCurrency()
    {
        var component = _allocator.Allocate([Batch(1m)], [new LandedCostComponentInput(5m, "EUR")], NoRates, "BDT");
        var purchase = _allocator.Allocate([Batch(1m, 1m, "EUR")], [], new Dictionary<string, decimal> { ["USD"] = 118.5m }, "BDT");

        Assert.Equal(BusinessViolationCodes.ExchangeRateMissing, component.ViolationCode);
        Assert.Contains("EUR", component.Message);
        Assert.Equal(BusinessViolationCodes.ExchangeRateMissing, purchase.ViolationCode);
        Assert.Contains("EUR", purchase.Message);
    }

    [Fact]
    public void U_G4_NoReceivedQuantity_IsRefused()
    {
        var none = _allocator.Allocate([], [new LandedCostComponentInput(5m, "BDT")], NoRates, "BDT");
        var zero = _allocator.Allocate([Batch(0m)], [new LandedCostComponentInput(5m, "BDT")], NoRates, "BDT");

        Assert.Equal(BusinessViolationCodes.NoReceivedQuantityToAllocate, none.ViolationCode);
        Assert.Equal(BusinessViolationCodes.NoReceivedQuantityToAllocate, zero.ViolationCode);
    }

    [Fact]
    public void NoCostComponents_StillGivesTheLandedUnitCostEqualToThePurchaseCost()
    {
        var result = _allocator.Allocate([Batch(4m, 2.5m)], [], NoRates, "BDT");

        var line = Assert.Single(result.Lines);
        Assert.Equal(0m, line.AllocatedCostBase);
        Assert.Equal(2.5000m, line.LandedUnitCostBase);
    }

    [Theory]
    [InlineData(1000.01, 7)]
    [InlineData(1.00, 3)]
    [InlineData(99999.99, 11)]
    public void TheAllocatedAmountsAlwaysSumToTheTotal_ForAnyNumberOfBatches(double total, int count)
    {
        var batches = Enumerable.Range(1, count).Select(i => Batch(i * 1.5m)).ToList();

        var result = _allocator.Allocate(batches, [new LandedCostComponentInput((decimal)total, "BDT")], NoRates, "BDT");

        Assert.Equal((decimal)total, result.Lines.Sum(l => l.AllocatedCostBase));
    }
}
