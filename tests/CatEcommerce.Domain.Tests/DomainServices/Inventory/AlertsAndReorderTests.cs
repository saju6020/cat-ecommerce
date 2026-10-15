using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.DomainServices.Inventory;

/// <summary>HO-IMP-001 §15 phase H -- U-H1 ... U-H6: the expiry thresholds (US-IMP-10 AC1) and the reorder suggestion (US-IMP-12).</summary>
public sealed class AlertsAndReorderTests
{
    private static readonly IReadOnlyList<int> Default = ExpiryAlertRules.Thresholds(90, 60, 30);

    [Fact]
    public void U_H1_ABatchRaisesOnlyTheTightestThresholdItHasCrossed_AndNeverARetrospectiveOne()
    {
        Assert.Equal(60, ExpiryAlertRules.TightestCrossed(45, Default));   // received with 45 days left: 60, not 90
        Assert.Equal(30, ExpiryAlertRules.TightestCrossed(29, Default));   // later: 30
        Assert.Null(ExpiryAlertRules.TightestCrossed(-1, Default));        // expired: the separate Expired alert, not a threshold
    }

    [Theory]
    [InlineData(91, null)]
    [InlineData(90, 90)]
    [InlineData(89, 90)]
    [InlineData(61, 90)]
    [InlineData(60, 60)]
    [InlineData(31, 60)]
    [InlineData(30, 30)]
    [InlineData(0, 30)]
    public void U_H1_TheBoundariesAreInclusive(int daysLeft, int? expected) => Assert.Equal(expected, ExpiryAlertRules.TightestCrossed(daysLeft, Default));

    [Fact]
    public void U_H2_ZeroSwitchesAThresholdOff_AndTheRestAreSortedDescendingAndDistinct()
    {
        Assert.Equal([90, 30], ExpiryAlertRules.Thresholds(90, 0, 30));
        Assert.Equal([60, 30], ExpiryAlertRules.Thresholds(30, 60, 30));
        Assert.Empty(ExpiryAlertRules.Thresholds(0, 0, 0));
        Assert.Null(ExpiryAlertRules.TightestCrossed(10, ExpiryAlertRules.Thresholds(0, 0, 0)));
    }

    private readonly ReorderSuggestionCalculator _calculator = new();

    [Fact]
    public void U_H3_TheSuggestionIsTheCoverDemandPlusThePointLessThePosition()
    {
        // avgDaily 2.0 (180 over 90 days), lead 10 + cover 14 = 24 days -> ceil(48), + point 10, - (usable 5 + on order 20) = 33
        var result = _calculator.Suggest(new ReorderInput(180m, 90, 10, 14, 10m, 5m, 20m, null));

        Assert.Equal(2.0m, result.AverageDailyConsumption);
        Assert.Equal(24, result.CoverDaysTotal);
        Assert.Equal(25m, result.Position);
        Assert.Equal(33m, result.SuggestedQuantity);
    }

    [Fact]
    public void U_H4_WithNoConsumption_TheSuggestionTopsUpToTheReorderPoint()
    {
        Assert.Equal(7m, _calculator.Suggest(new ReorderInput(0m, 90, 14, 14, 10m, 3m, 0m, null)).SuggestedQuantity);
    }

    [Fact]
    public void U_H4_ThePositionAboveWhatIsNeeded_SuggestsNothing_NeverANegative()
    {
        Assert.Equal(0m, _calculator.Suggest(new ReorderInput(0m, 90, 14, 14, 10m, 50m, 0m, null)).SuggestedQuantity);
    }

    [Fact]
    public void U_H4_ADemandThatIsNotWholeIsRoundedUpToWholeUnits()
    {
        // 10 over 90 days = 0.1111/day x 28 days = 3.11 -> 4
        Assert.Equal(4m, _calculator.Suggest(new ReorderInput(10m, 90, 14, 14, 0m, 0m, 0m, null)).SuggestedQuantity);
    }

    [Fact]
    public void U_H5_APolicyQuantityWinsOverTheFormula()
    {
        Assert.Equal(50m, _calculator.Suggest(new ReorderInput(180m, 90, 10, 14, 10m, 5m, 20m, 50m)).SuggestedQuantity);
    }

    [Fact]
    public void U_H6_UsableExcludesExpiredQuarantinedRecalledAndReservedStock()
    {
        var today = new DateOnly(2026, 10, 7);

        Assert.Equal(7m, StockUsability.Usable(10m, 3m, BatchStatus.Available, StockLocationKind.ShopFloor, today.AddDays(30), today)); // reserved is promised
        Assert.Equal(0m, StockUsability.Usable(10m, 0m, BatchStatus.Available, StockLocationKind.ShopFloor, today.AddDays(-1), today));  // expired
        Assert.Equal(10m, StockUsability.Usable(10m, 0m, BatchStatus.Available, StockLocationKind.ShopFloor, today, today));            // the expiry day itself is still usable
        Assert.Equal(0m, StockUsability.Usable(10m, 0m, BatchStatus.Quarantined, StockLocationKind.ShopFloor, null, today));
        Assert.Equal(0m, StockUsability.Usable(10m, 0m, BatchStatus.Recalled, StockLocationKind.ShopFloor, null, today));
        Assert.Equal(0m, StockUsability.Usable(10m, 0m, BatchStatus.WrittenOff, StockLocationKind.ShopFloor, null, today));
        Assert.Equal(0m, StockUsability.Usable(10m, 0m, BatchStatus.Available, StockLocationKind.Quarantine, null, today));
        Assert.Equal(0m, StockUsability.Usable(2m, 5m, BatchStatus.Available, StockLocationKind.ShopFloor, null, today));              // never negative
    }
}
