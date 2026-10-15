using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Tests.Ordering;

/// <summary>HO-ORD-001 §15 group B (zones, charges, slots, payment options) -- US-ORD-06, US-ORD-07.</summary>
public sealed class ZoneAndChargeTests
{
    private static readonly UserContext User = new(Guid.NewGuid(), Guid.NewGuid());

    private static ResolvableZone Zone(string name, int priority, bool active, params (string District, string[] Codes)[] coverage) =>
        new(Guid.NewGuid(), name, priority, active, coverage.Select(c => new CoverageRule(c.District, c.Codes)).ToList());

    // ── U-B1 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_B1_APostalCodeZone_BeatsADistrictWideZone()
    {
        var wide = Zone("District wide", 0, true, ("Dhaka", []));
        var specific = Zone("Gulshan only", 5, true, ("Dhaka", ["1212", "1213"]));

        var resolved = DeliveryZoneResolver.Resolve([wide, specific], "dhaka", "1212");
        Assert.Equal(specific.ZoneId, resolved!.ZoneId);

        // an address outside the listed postal codes falls back to the district-wide zone
        Assert.Equal(wide.ZoneId, DeliveryZoneResolver.Resolve([wide, specific], "Dhaka", "1000")!.ZoneId);
    }

    [Fact]
    public void U_B1_NoMatch_ResolvesNothing_SoDeliveryIsUnavailableAndPickupOffered()
    {
        var zone = Zone("Dhaka", 0, true, ("Dhaka", []));

        Assert.Null(DeliveryZoneResolver.Resolve([zone], "Chattogram", "4000"));
        Assert.Null(DeliveryZoneResolver.Resolve([zone], "", "1212"));
        Assert.Null(DeliveryZoneResolver.Resolve([], "Dhaka", "1212"));
    }

    [Fact]
    public void U_B1_TheDistrictIsComparedIgnoringCaseAndWhitespace()
    {
        var zone = Zone("Dhaka", 0, true, ("  Dhaka   North ", []));

        Assert.NotNull(DeliveryZoneResolver.Resolve([zone], "dhaka north", null));
        Assert.NotNull(DeliveryZoneResolver.Resolve([zone], "DHAKA\tNORTH", null));
    }

    [Fact]
    public void U_B1_AnInactiveZone_NeverResolves_AndTiesGoToPriorityThenName()
    {
        var inactive = Zone("Inactive", 0, false, ("Dhaka", []));
        var second = Zone("B zone", 2, true, ("Dhaka", []));
        var first = Zone("Z zone", 1, true, ("Dhaka", []));
        var alsoFirst = Zone("A zone", 1, true, ("Dhaka", []));

        Assert.Equal(alsoFirst.ZoneId, DeliveryZoneResolver.Resolve([inactive, second, first, alsoFirst], "Dhaka", null)!.ZoneId);
        Assert.Null(DeliveryZoneResolver.Resolve([inactive], "Dhaka", null));
    }

    // ── U-B2 / U-B3 ─────────────────────────────────────────────────────────────────────────────────
    private static readonly ZoneChargeRules Standard = new(60m, null, [new WeightBand(2000, 0m), new WeightBand(5000, 40m), new WeightBand(null, 100m)]);

    [Fact]
    public void U_B2_Base60_Band5000Plus40_For3_2kg_Is100() =>
        Assert.Equal(10_000L, DeliveryChargeCalculator.Calculate(Standard, new MinorUnits(100_000), 3200).Value);

    [Theory]
    [InlineData(0L, 6_000L)]
    [InlineData(2000L, 6_000L)]   // on the band limit: still the first band
    [InlineData(2001L, 10_000L)]
    [InlineData(5000L, 10_000L)]
    [InlineData(5001L, 16_000L)]  // the open-ended band
    [InlineData(900_000L, 16_000L)]
    public void U_B2_TheFirstBandWhoseLimitIsNotExceeded_Applies(long grams, long expectedCharge) =>
        Assert.Equal(expectedCharge, DeliveryChargeCalculator.Calculate(Standard, new MinorUnits(100_000), grams).Value);

    [Fact]
    public void U_B3_FreeOver2000_ChargedAt1999_99_FreeAt2000_00()
    {
        var rules = new ZoneChargeRules(60m, 2000m, []);

        Assert.Equal(6_000L, DeliveryChargeCalculator.Calculate(rules, MinorUnits.FromAmount(1999.99m), 0).Value);
        Assert.Equal(0L, DeliveryChargeCalculator.Calculate(rules, MinorUnits.FromAmount(2000.00m), 0).Value);
    }

    [Fact]
    public void U_B3_ExpressUsesItsOwnRules_NotStandards()
    {
        var express = new ZoneChargeRules(150m, 5000m, [new WeightBand(null, 25m)]);
        var goods = MinorUnits.FromAmount(2500m);

        Assert.Equal(6_000L, DeliveryChargeCalculator.Calculate(Standard, goods, 100).Value);
        Assert.Equal(17_500L, DeliveryChargeCalculator.Calculate(express, goods, 100).Value);
        Assert.Equal(0L, DeliveryChargeCalculator.Calculate(express, MinorUnits.FromAmount(5000m), 100).Value);
    }

    [Fact]
    public void ANoBandServiceLevel_ChargesTheBaseOnly() =>
        Assert.Equal(6_000L, DeliveryChargeCalculator.Calculate(new ZoneChargeRules(60m, null, []), new MinorUnits(1), 99_999).Value);

    // ── U-B4 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_B4_WindowsMonToSat_LeadTime1_AClosureTomorrow_TomorrowIsAbsent_SevenDatesListed()
    {
        var windows = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday }
            .Select(d => new SlotWindow(d, new TimeOnly(10, 0), new TimeOnly(13, 0))).ToList();

        var today = new DateOnly(2026, 10, 12); // a Monday
        var tomorrow = today.AddDays(1);

        var slots = DeliverySlotCalculator.Slots(windows, 1, today, date => date != tomorrow);

        Assert.DoesNotContain(slots, s => s.Date == tomorrow);
        Assert.Equal(7, slots.Select(s => s.Date).Distinct().Count());
        Assert.DoesNotContain(slots, s => s.Date.DayOfWeek == DayOfWeek.Sunday);
        Assert.All(slots, s => Assert.True(s.Date >= today.AddDays(1)));
        Assert.Equal("10:00–13:00", slots[0].Label);
    }

    [Fact]
    public void U_B4_NoWindows_NoSlotChoice_AndTheLeadTimeStartsTheListing()
    {
        Assert.Empty(DeliverySlotCalculator.Slots([], 1, new DateOnly(2026, 10, 12), _ => true));

        var daily = Enum.GetValues<DayOfWeek>().Select(d => new SlotWindow(d, new TimeOnly(9, 0), new TimeOnly(12, 0))).ToList();
        var today = new DateOnly(2026, 10, 12);
        Assert.Equal(today.AddDays(3), DeliverySlotCalculator.Slots(daily, 3, today, _ => true)[0].Date);
    }

    [Fact]
    public void U_B4_AChosenSlot_IsOffered_OnlyWhenItIsInTheListing()
    {
        var window = new SlotWindow(DayOfWeek.Wednesday, new TimeOnly(10, 0), new TimeOnly(13, 0));
        var today = new DateOnly(2026, 10, 12);
        var wednesday = new DateOnly(2026, 10, 14);

        Assert.True(DeliverySlotCalculator.IsOffered([window], 1, today, _ => true, wednesday, new TimeOnly(10, 0)));
        Assert.False(DeliverySlotCalculator.IsOffered([window], 1, today, _ => true, wednesday, new TimeOnly(11, 0)));       // a window that does not exist
        Assert.False(DeliverySlotCalculator.IsOffered([window], 1, today, _ => true, new DateOnly(2026, 10, 13), new TimeOnly(10, 0))); // a Tuesday
        Assert.False(DeliverySlotCalculator.IsOffered([window], 3, today, _ => true, wednesday, new TimeOnly(10, 0)));       // before the lead time
        Assert.False(DeliverySlotCalculator.IsOffered([window], 1, today, d => d != wednesday, wednesday, new TimeOnly(10, 0))); // closed
    }

    // ── U-B5 ────────────────────────────────────────────────────────────────────────────────────────
    private static PaymentOptionInputs Inputs(
        FulfilmentMethod fulfilment = default!, bool cod = true, decimal codMax = 5000m, bool? zoneCod = true, decimal? zoneMax = null, long total = 100_000,
        bool transfer = true, bool account = true, bool collect = true) =>
        new(fulfilment ?? FulfilmentMethod.Delivery, cod, codMax, zoneCod, zoneMax, new MinorUnits(total), transfer, account, collect, "BDT");

    private static PaymentOption Option(PaymentOptionInputs inputs, OrderPaymentMethod method) => PaymentOptionPolicy.For(inputs, method);

    [Fact]
    public void U_B5_AllThreeAvailable_ForADeliveryOrder_ExceptPayOnCollection()
    {
        var options = PaymentOptionPolicy.Evaluate(Inputs());

        Assert.True(Option(Inputs(), OrderPaymentMethod.CashOnDelivery).Available);
        Assert.True(Option(Inputs(), OrderPaymentMethod.BankTransfer).Available);
        Assert.False(Option(Inputs(), OrderPaymentMethod.PayOnCollection).Available);
        Assert.Equal(3, options.Count);
        Assert.DoesNotContain(options, o => o.Method == OrderPaymentMethod.InStore);
    }

    [Fact]
    public void U_B5_Pickup_OffersPayOnCollection_NotCod()
    {
        var pickup = Inputs(FulfilmentMethod.Pickup, zoneCod: null);

        Assert.Equal("NotForPickup", Option(pickup, OrderPaymentMethod.CashOnDelivery).UnavailableReason);
        Assert.True(Option(pickup, OrderPaymentMethod.PayOnCollection).Available);
        Assert.True(Option(pickup, OrderPaymentMethod.BankTransfer).Available);
    }

    [Fact]
    public void U_B5_CodDisabled_ByTheBranchSetting() =>
        Assert.Equal("CodDisabled", Option(Inputs(cod: false), OrderPaymentMethod.CashOnDelivery).UnavailableReason);

    [Fact]
    public void U_B5_CodNotOfferedInTheZone() =>
        Assert.Equal("CodNotOfferedInZone", Option(Inputs(zoneCod: false), OrderPaymentMethod.CashOnDelivery).UnavailableReason);

    [Fact]
    public void U_B5_CodAboveTheCeiling_NamesTheLimit()
    {
        var option = Option(Inputs(total: 500_001), OrderPaymentMethod.CashOnDelivery);

        Assert.False(option.Available);
        Assert.Equal("CodAboveOrderLimit", option.UnavailableReason);
        Assert.Contains("BDT 5,000.00", option.Message);

        Assert.True(Option(Inputs(total: 500_000), OrderPaymentMethod.CashOnDelivery).Available); // exactly the ceiling is allowed
    }

    [Fact]
    public void U_B5_TheZonesOwnCeiling_WinsOverTheSetting()
    {
        var zoneCeiling = Inputs(total: 200_000, zoneMax: 1000m);
        Assert.Equal("CodAboveOrderLimit", Option(zoneCeiling, OrderPaymentMethod.CashOnDelivery).UnavailableReason);
        Assert.Contains("BDT 1,000.00", Option(zoneCeiling, OrderPaymentMethod.CashOnDelivery).Message);

        Assert.True(Option(Inputs(total: 800_000, zoneMax: 10_000m), OrderPaymentMethod.CashOnDelivery).Available);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void U_B5_BankTransferDisabled_WhenSwitchedOffOrThereIsNoActiveAccount(bool setting, bool account) =>
        Assert.Equal("BankTransferDisabled", Option(Inputs(transfer: setting, account: account), OrderPaymentMethod.BankTransfer).UnavailableReason);

    [Fact]
    public void U_B5_PayOnCollectionDisabled_AndNotForDelivery()
    {
        Assert.Equal("PayOnCollectionDisabled", Option(Inputs(FulfilmentMethod.Pickup, zoneCod: null, collect: false), OrderPaymentMethod.PayOnCollection).UnavailableReason);
        Assert.Equal("NotForDelivery", Option(Inputs(), OrderPaymentMethod.PayOnCollection).UnavailableReason);
    }

    // ── the aggregates' own shape rules ─────────────────────────────────────────────────────────────
    private static ZoneDto ValidZone(Action<List<ZoneServiceDto>>? services = null, Action<List<CoverageDto>>? coverage = null)
    {
        var serviceList = new List<ZoneServiceDto>
        {
            new(DeliveryServiceLevel.Standard, true, 60m, 2000m, 1, [new WeightBand(2000, 0m), new WeightBand(null, 40m)],
                [new SlotWindow(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(13, 0))]),
        };
        var coverageList = new List<CoverageDto> { new("Dhaka", []) };
        services?.Invoke(serviceList);
        coverage?.Invoke(coverageList);

        return new ZoneDto(Guid.NewGuid(), "Dhaka city", 1, true, true, null, "BDT", coverageList, serviceList, User, Guid.NewGuid());
    }

    private static DomainRuleViolationException Invalid(ZoneDto dto) =>
        Assert.Throws<DomainRuleViolationException>(() => new DeliveryZoneAggregate(Guid.NewGuid()).Create(dto));

    [Fact]
    public void AValidZone_IsCreated_AndRaisesOneEvent()
    {
        var zone = new DeliveryZoneAggregate(Guid.NewGuid());
        zone.Create(ValidZone());

        Assert.Equal("Dhaka city", zone.Name);
        Assert.Single(zone.Coverage);
        Assert.NotNull(zone.ServiceFor(DeliveryServiceLevel.Standard));
        Assert.Null(zone.ServiceFor(DeliveryServiceLevel.Express));
        Assert.Single(zone.Events);
        Assert.Equal(zone.ToResolvable().ZoneId, zone.Id);
    }

    [Fact]
    public void AZone_NeedsANameACoverageRowAndAServiceLevel()
    {
        Assert.Equal(BusinessViolationCodes.InvalidDeliveryZone, Invalid(ValidZone() with { Name = "  " }).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidDeliveryZone, Invalid(ValidZone(coverage: c => c.Clear())).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidDeliveryZone, Invalid(ValidZone(services: s => s.Clear())).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidDeliveryZone, Invalid(ValidZone() with { Priority = -1 }).ErrorCode);
    }

    [Fact]
    public void AZone_RefusesDuplicateServiceLevels_BadAmounts_BadBands_BadWindowsAndBadLeadTimes()
    {
        Assert.Equal(BusinessViolationCodes.InvalidDeliveryZone, Invalid(ValidZone(s => s.Add(s[0]))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidDeliveryZone, Invalid(ValidZone(s => s[0] = s[0] with { BaseCharge = -1m })).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidDeliveryZone, Invalid(ValidZone(s => s[0] = s[0] with { BaseCharge = 10.123m })).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidDeliveryZone, Invalid(ValidZone(s => s[0] = s[0] with { LeadTimeDays = 15 })).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidDeliveryZone, Invalid(ValidZone(s => s[0] = s[0] with { WeightBands = [new WeightBand(null, 1m), new WeightBand(2000, 0m)] })).ErrorCode); // open-ended not last
        Assert.Equal(BusinessViolationCodes.InvalidDeliveryZone, Invalid(ValidZone(s => s[0] = s[0] with { WeightBands = [new WeightBand(3000, 1m), new WeightBand(2000, 0m)] })).ErrorCode); // not ascending
        Assert.Equal(BusinessViolationCodes.InvalidDeliveryZone, Invalid(ValidZone(s => s[0] = s[0] with
        {
            SlotWindows = [new SlotWindow(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(13, 0)), new SlotWindow(DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(15, 0))],
        })).ErrorCode); // overlap
        Assert.Equal(BusinessViolationCodes.InvalidDeliveryZone, Invalid(ValidZone(s => s[0] = s[0] with
        {
            SlotWindows = [new SlotWindow(DayOfWeek.Monday, new TimeOnly(13, 0), new TimeOnly(10, 0))],
        })).ErrorCode); // end before start
        Assert.Equal(BusinessViolationCodes.InvalidDeliveryZone, Invalid(ValidZone() with { CodMaxOrderValue = -5m }).ErrorCode);
    }

    [Fact]
    public void AZone_CannotMoveBranch_ButCanBeEditedInPlace_AndDeactivated()
    {
        var zone = new DeliveryZoneAggregate(Guid.NewGuid());
        var created = ValidZone();
        zone.Create(created);
        zone.ClearEvents();

        Assert.Equal(BusinessViolationCodes.InvalidDeliveryZone,
            Assert.Throws<DomainRuleViolationException>(() => zone.Update(created with { BranchId = Guid.NewGuid() })).ErrorCode);

        zone.Update(created with { Name = "Renamed", CodEnabled = false });
        Assert.Equal("Renamed", zone.Name);
        Assert.False(zone.CodEnabled);

        zone.SetActivation(false, User, Guid.NewGuid());
        Assert.False(zone.IsActive);
        Assert.Equal(2, zone.Events.Count);
    }

    [Fact]
    public void ACourier_NeedsAName_AndAnHttpsTemplateWithTheTrackingPlaceholderOnce()
    {
        var courier = new CourierAggregate(Guid.NewGuid());
        courier.Create("Sundarban", "https://track.example.com/t?n={tracking}", true, true, User);
        Assert.Equal("https://track.example.com/t?n=AB%2F12", courier.BuildTrackingUrl("AB/12"));
        Assert.Null(courier.BuildTrackingUrl(" "));

        foreach (var bad in new[] { "http://track.example.com/{tracking}", "https://track.example.com/", "https://x.example.com/{tracking}/{tracking}", "https://user:pw@x.example.com/{tracking}", "https://x.example.com/ {tracking}", "javascript:alert({tracking})" })
        {
            var ex = Assert.Throws<DomainRuleViolationException>(() => new CourierAggregate(Guid.NewGuid()).Create("C", bad, false, true, User));
            Assert.Equal(BusinessViolationCodes.InvalidCourier, ex.ErrorCode);
        }

        Assert.Throws<DomainRuleViolationException>(() => new CourierAggregate(Guid.NewGuid()).Create(" ", null, false, true, User));
        Assert.Null(new CourierAggregate(Guid.NewGuid()).Also(c => c.Create("Manual", null, false, true, User)).BuildTrackingUrl("X1"));
    }

    [Fact]
    public void TheBankTransferAccount_IsASingleton_WithAllRequiredFields()
    {
        var account = new BankTransferAccountAggregate(BankTransferAccountAggregate.SingletonId);
        account.Create("Purr Care Ltd", "1234567890", "Sample Bank", "Gulshan", " 012345678 ", true, User);
        Assert.Equal("012345678", account.RoutingNumber);

        foreach (var blank in new[] { "", "  " })
        {
            var ex = Assert.Throws<DomainRuleViolationException>(() => new BankTransferAccountAggregate(Guid.NewGuid()).Create(blank, "1", "b", "br", null, true, User));
            Assert.Equal(BusinessViolationCodes.InvalidBankTransferAccount, ex.ErrorCode);
        }
    }
}

internal static class ZoneTestExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
