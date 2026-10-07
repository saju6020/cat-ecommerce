using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-SHP-001 §15 U-C2, U-C3.</summary>
public sealed class SkuAvailabilityRulesTests
{
    [Theory]
    [InlineData(3, true, true, "InStock")]
    [InlineData(0, true, true, "OutOfStock")]
    [InlineData(null, true, true, "OutOfStock")]
    [InlineData(5, false, true, "OutOfStock")] // discontinued SKU
    [InlineData(5, true, false, "OutOfStock")] // archived/unlisted product
    public void Evaluate_Cases(int? sellable, bool skuActive, bool productSellable, string expected)
    {
        var result = SkuAvailabilityRules.Evaluate(sellable, skuActive, productSellable);
        Assert.Equal(expected, result.DisplayName);
    }

    [Fact]
    public void ExpiryNotice_WithinWindow_Shown()
    {
        var today = new DateOnly(2026, 1, 1);
        var notice = SkuAvailabilityRules.ExpiryNotice(today.AddDays(60), today, 60);
        Assert.Equal(today.AddDays(60), notice);
    }

    [Fact]
    public void ExpiryNotice_OutsideWindow_NotShown()
    {
        var today = new DateOnly(2026, 1, 1);
        var notice = SkuAvailabilityRules.ExpiryNotice(today.AddDays(61), today, 60);
        Assert.Null(notice);
    }

    [Fact]
    public void ExpiryNotice_WindowZero_NeverShown()
    {
        var today = new DateOnly(2026, 1, 1);
        var notice = SkuAvailabilityRules.ExpiryNotice(today, today, 0);
        Assert.Null(notice);
    }

    [Fact]
    public void ExpiryNotice_NullExpiry_None()
    {
        var today = new DateOnly(2026, 1, 1);
        Assert.Null(SkuAvailabilityRules.ExpiryNotice(null, today, 60));
    }

    [Fact]
    public void EvaluateBundle_EachComponentHasEnough_InStock()
    {
        var components = new (int, int?)[] { (2, 5), (1, 1) };
        Assert.Equal(AvailabilityStatus.InStock, SkuAvailabilityRules.EvaluateBundle(components));
    }

    [Fact]
    public void EvaluateBundle_OneComponentOutOfStock_OutOfStock()
    {
        var components = new (int, int?)[] { (2, 5), (1, 0) };
        Assert.Equal(AvailabilityStatus.OutOfStock, SkuAvailabilityRules.EvaluateBundle(components));
    }

    [Fact]
    public void EvaluateBundle_NotEnoughForOneUnit_OutOfStock()
    {
        var components = new (int, int?)[] { (2, 1) }; // floor(1/2) = 0
        Assert.Equal(AvailabilityStatus.OutOfStock, SkuAvailabilityRules.EvaluateBundle(components));
    }
}
