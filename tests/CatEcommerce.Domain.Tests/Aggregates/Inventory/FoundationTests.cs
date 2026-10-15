using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Aggregates.Inventory;

/// <summary>HO-IMP-001 §15 phase A -- U-A1 ... U-A5: the rules tables, the quantity and cost value objects, the storage profile and the
/// branch-local date.</summary>
public sealed class FoundationTests
{
    private static InventoryActor Actor() => new(TestUserContext.Create(), Guid.NewGuid());

    // ── U-A1 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>§8.3's table, transcribed independently of <see cref="StockLocationRules"/>: what each kind permits BEYOND the movements that
    /// are allowed everywhere.</summary>
    private static readonly IReadOnlyDictionary<StockLocationKind, MovementKind[]> KindSpecific = new Dictionary<StockLocationKind, MovementKind[]>
    {
        [StockLocationKind.ShopFloor] = [MovementKind.Sale],
        [StockLocationKind.Warehouse] = [MovementKind.Sale],
        [StockLocationKind.ClinicDispensary] = [MovementKind.Dispense, MovementKind.ClinicalUse],
        [StockLocationKind.BoardingStore] = [MovementKind.BoardingUse],
        [StockLocationKind.Quarantine] = [MovementKind.Return],
    };

    private static readonly MovementKind[] Everywhere =
    [
        MovementKind.Receipt, MovementKind.TransferIn, MovementKind.TransferOut, MovementKind.AdjustmentIn, MovementKind.AdjustmentOut,
        MovementKind.CountGain, MovementKind.CountLoss, MovementKind.Wastage,
    ];

    public static IEnumerable<object[]> KindByMovement() =>
        from kind in Enumeration.GetAll<StockLocationKind>()
        from movement in Enumeration.GetAll<MovementKind>()
        select new object[] { kind, movement };

    [Theory]
    [MemberData(nameof(KindByMovement))]
    public void U_A1_StockLocationRules_IsExactlyTheTableOfSection8_3(StockLocationKind kind, MovementKind movement)
    {
        var expected = Everywhere.Contains(movement) || KindSpecific[kind].Contains(movement);

        Assert.Equal(expected, StockLocationRules.Permits(kind, movement));
    }

    [Fact]
    public void U_A1_OnlyShopFloorAndWarehouseCountTowardRetailSellable_AndFefoPriorityIs1_2_3()
    {
        Assert.True(StockLocationRules.CountsTowardRetailSellable(StockLocationKind.ShopFloor));
        Assert.True(StockLocationRules.CountsTowardRetailSellable(StockLocationKind.Warehouse));
        Assert.False(StockLocationRules.CountsTowardRetailSellable(StockLocationKind.ClinicDispensary));
        Assert.False(StockLocationRules.CountsTowardRetailSellable(StockLocationKind.BoardingStore));
        Assert.False(StockLocationRules.CountsTowardRetailSellable(StockLocationKind.Quarantine));

        Assert.Equal(1, StockLocationRules.FefoPriority(StockLocationKind.Warehouse));
        Assert.Equal(2, StockLocationRules.FefoPriority(StockLocationKind.ShopFloor));
        Assert.Equal(3, StockLocationRules.FefoPriority(StockLocationKind.Quarantine));
    }

    [Fact]
    public void U_A1_TheFiveSeededLocationsHaveDistinctIdsAndCodes()
    {
        Guid[] ids = [InventorySeed.ShopLocationId, InventorySeed.WarehouseLocationId, InventorySeed.DispensaryLocationId, InventorySeed.BoardingStoreLocationId, InventorySeed.QuarantineLocationId];
        string[] codes = [InventorySeed.ShopCode, InventorySeed.WarehouseCode, InventorySeed.DispensaryCode, InventorySeed.BoardingStoreCode, InventorySeed.QuarantineCode];

        Assert.Equal(5, ids.Distinct().Count());
        Assert.Equal(5, codes.Distinct().Count());
    }

    // ── U-A2 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.2345")]
    public void U_A2_StockQuantity_RefusesZeroNegativeAndMoreThanThreeDecimals(string text)
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => StockQuantity.Create(decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture), allowsFractionalQuantity: true));

        Assert.Equal(BusinessViolationCodes.InvalidQuantity, ex.ErrorCode);
    }

    [Fact]
    public void U_A2_StockQuantity_FractionalOnAWholeOnlySku_IsFractionalQuantityNotAllowed()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => StockQuantity.Create(1.5m, allowsFractionalQuantity: false));

        Assert.Equal(BusinessViolationCodes.FractionalQuantityNotAllowed, ex.ErrorCode);
    }

    [Fact]
    public void U_A2_StockQuantity_AcceptsWholeAndThreeDecimalQuantities()
    {
        Assert.Equal(12m, StockQuantity.Create(12m, allowsFractionalQuantity: false).Value);
        Assert.Equal(0.125m, StockQuantity.Create(0.125m, allowsFractionalQuantity: true).Value);
    }

    // ── U-A3 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_A3_UnitCost_KeepsFourDecimals()
    {
        Assert.Equal(12.3456m, new UnitCost(12.3456m, "USD").Amount);
    }

    [Fact]
    public void U_A3_UnitCost_NegativeIsRefused()
    {
        Assert.Throws<DomainRuleViolationException>(() => new UnitCost(-0.0001m, "USD"));
    }

    [Fact]
    public void U_A3_UnitCost_HasNoDoubleOrFloatAnywhere()
    {
        var members = typeof(UnitCost).GetConstructors().SelectMany(c => c.GetParameters().Select(p => p.ParameterType))
            .Concat(typeof(UnitCost).GetProperties().Select(p => p.PropertyType))
            .Concat(typeof(UnitCost).GetMethods().Where(m => m.DeclaringType == typeof(UnitCost)).Select(m => m.ReturnType));

        Assert.DoesNotContain(typeof(double), members);
        Assert.DoesNotContain(typeof(float), members);
    }

    // ── U-A4 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static StorageProfileDto Profile(decimal? min, decimal? max) => new(StorageCondition.Refrigerated, min, max, null, Actor());

    [Theory]
    [InlineData(8, 2)]
    [InlineData(-50, null)]
    [InlineData(null, 61)]
    public void U_A4_StorageProfile_RefusesAnInvertedOrOutOfBoundsRange(int? min, int? max)
    {
        var profile = new ProductStorageProfileAggregate(Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() => profile.Set(Profile(min, max)));

        Assert.Equal(BusinessViolationCodes.InvalidTemperatureRange, ex.ErrorCode);
    }

    [Fact]
    public void U_A4_StorageProfile_AcceptsAValidRange_AndKeepsOneDecimal()
    {
        var profile = new ProductStorageProfileAggregate(Guid.NewGuid());

        profile.Set(Profile(2.04m, 8m));

        Assert.Equal(StorageCondition.Refrigerated, profile.Condition);
        Assert.Equal(2.0m, profile.MinTemperatureC);
        Assert.Equal(8m, profile.MaxTemperatureC);
        Assert.Single(profile.Events);
    }

    // ── U-A5 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_A5_BranchCalendar_LocalDateOf_DhakaIsSixHoursAhead()
    {
        var instant = new DateTimeOffset(2026, 10, 11, 18, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 10, 12), BranchCalendar.LocalDateOf(instant, "Asia/Dhaka"));
        Assert.Equal(new DateOnly(2026, 10, 11), BranchCalendar.LocalDateOf(instant, "Europe/London"));
    }
}
