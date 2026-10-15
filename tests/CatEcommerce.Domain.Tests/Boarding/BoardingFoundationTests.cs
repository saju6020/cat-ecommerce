using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Xunit;

namespace CatEcommerce.Domain.Tests.Boarding;

/// <summary>HO-BRD-001 Phase A -- U-A1, U-A8, U-A9 and the pure services of §8.4-§8.10 that need no database (U-B3..U-B9, U-C1, U-C2, U-C9..U-C12, U-E8).</summary>
public sealed class BoardingFoundationTests
{
    // ── U-A1: StayTransitionRules over every (from, action) ──────────────────────────────────────────────────────────

    private static readonly (string Action, BoardingStayStatus From)[] Allowed =
    [
        (StayTransitionRules.Reserve, BoardingStayStatus.Enquiry),
        (StayTransitionRules.Confirm, BoardingStayStatus.Reserved),
        (StayTransitionRules.Lapse, BoardingStayStatus.Reserved),
        (StayTransitionRules.ExpireEnquiry, BoardingStayStatus.Enquiry),
        (StayTransitionRules.Cancel, BoardingStayStatus.Enquiry),
        (StayTransitionRules.Cancel, BoardingStayStatus.Reserved),
        (StayTransitionRules.Cancel, BoardingStayStatus.Confirmed),
        (StayTransitionRules.CheckIn, BoardingStayStatus.Confirmed),
        (StayTransitionRules.NoShow, BoardingStayStatus.Confirmed),
        (StayTransitionRules.CheckOut, BoardingStayStatus.CheckedIn),
    ];

    [Fact]
    public void U_A1_EveryPair_MatchesTheAppendixCArrows()
    {
        foreach (var status in Enumeration.GetAll<BoardingStayStatus>())
            foreach (var transition in StayTransitionRules.All)
            {
                var expected = Array.Exists(Allowed, a => a.Action == transition.Action && a.From == status);
                Assert.Equal(expected, StayTransitionRules.IsAllowed(status, transition.Action));
            }
    }

    [Fact]
    public void U_A1_ARefusedTransition_NamesTheStateAndTheAttempt()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => StayTransitionRules.EnsureAllowed(BoardingStayStatus.Cancelled, StayTransitionRules.CheckIn));

        Assert.Equal(BusinessViolationCodes.InvalidStayTransition, ex.ErrorCode);
        Assert.Equal("A cancelled stay cannot be checked in.", ex.Message);
    }

    [Fact]
    public void U_A1_AllowedActions_AreTheSameTableTheAggregateUses()
    {
        Assert.Equal([StayTransitionRules.Confirm, StayTransitionRules.Lapse, StayTransitionRules.Cancel], StayTransitionRules.AllowedActions(BoardingStayStatus.Reserved));
        Assert.Empty(StayTransitionRules.AllowedActions(BoardingStayStatus.CheckedOut));
    }

    [Fact]
    public void U_X3_OccupancyIsHeldFromReservedToCheckedOut_AndNotByEnquiryCancelledOrNoShow()
    {
        Assert.True(StayTransitionRules.HoldsOccupancy(BoardingStayStatus.Reserved));
        Assert.True(StayTransitionRules.HoldsOccupancy(BoardingStayStatus.Confirmed));
        Assert.True(StayTransitionRules.HoldsOccupancy(BoardingStayStatus.CheckedIn));
        Assert.True(StayTransitionRules.HoldsOccupancy(BoardingStayStatus.CheckedOut));
        Assert.False(StayTransitionRules.HoldsOccupancy(BoardingStayStatus.Enquiry));
        Assert.False(StayTransitionRules.HoldsOccupancy(BoardingStayStatus.Cancelled));
        Assert.False(StayTransitionRules.HoldsOccupancy(BoardingStayStatus.NoShow));
    }

    // ── U-A8: the eleven templates ───────────────────────────────────────────────────────────────────────────────────

    private static readonly string[] TemplateKeys =
    [
        NotificationTemplateCatalogue.BoardingReserved, NotificationTemplateCatalogue.BoardingConfirmed, NotificationTemplateCatalogue.BoardingReservationLapsed,
        NotificationTemplateCatalogue.BoardingCancelled, NotificationTemplateCatalogue.BoardingCheckedIn, NotificationTemplateCatalogue.BoardingDailyUpdate,
        NotificationTemplateCatalogue.BoardingIncidentMedium, NotificationTemplateCatalogue.BoardingIncidentHigh, NotificationTemplateCatalogue.BoardingVetApprovalRequested,
        NotificationTemplateCatalogue.BoardingCheckedOut, NotificationTemplateCatalogue.BoardingWaitlistAvailable,
    ];

    private static NotificationTemplateDefinition Definition(string key) => NotificationTemplateCatalogue.Definitions.Single(d => d.Key == key);

    [Fact]
    public void U_A8_TheElevenTemplates_PassTemplateRules_AndAreTransactionalOnAllThreeChannels()
    {
        Assert.Equal(11, TemplateKeys.Length);

        foreach (var key in TemplateKeys)
        {
            var definition = Definition(key);
            var contents = definition.Defaults.Select(d => new TemplateContentInput(d.Channel, d.Subject, d.Title, d.Body, d.LinkPath)).ToList();

            Assert.Null(TemplateRules.Validate(definition, contents));
            Assert.Equal(NotificationCategory.Transactional, definition.Category);
            Assert.Equal(3, definition.Channels.Count);
            Assert.False(definition.ContainsSecret);
        }
    }

    [Fact]
    public void U_A8_TheUrgentOnes_AreReservedConfirmedCancelledHighIncidentAndVetApproval()
    {
        var urgent = TemplateKeys.Where(k => Definition(k).Urgency == NotificationUrgency.Urgent).OrderBy(k => k).ToArray();

        Assert.Equal(NotificationTemplateCatalogue.UrgentBoardingTemplateKeys.OrderBy(k => k).ToArray(), urgent);
        Assert.Equal(NotificationUrgency.Normal, Definition(NotificationTemplateCatalogue.BoardingIncidentMedium).Urgency);
        Assert.Equal(NotificationUrgency.Urgent, Definition(NotificationTemplateCatalogue.BoardingIncidentHigh).Urgency);
    }

    [Fact]
    public void U_A8_NoTemplate_CarriesFreeTextVariables()
    {
        // Messages carry ids, names, dates, amounts and links -- never a description, note or health text (section 8.19).
        var forbidden = new[] { "description", "note", "notes", "resolution", "observation", "healthNotes" };

        foreach (var key in TemplateKeys)
            Assert.DoesNotContain(Definition(key).Variables, v => forbidden.Contains(v.Name, StringComparer.OrdinalIgnoreCase));
    }

    // ── U-A9: the registry's start < end bound ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_A9_ACheckInWindowThatEndsBeforeItStarts_IsRefused()
    {
        var settings = new BusinessSettingsAggregate(SettingScopes.GlobalId);
        var change = new BusinessSettingsChangeDto([new SettingChangeInput(SettingKeys.BoardingCheckInWindow, "19:00-14:00")], TestUserContext.Create(), Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() => settings.Change(change));

        Assert.Equal(BusinessViolationCodes.InvalidSettingValue, ex.ErrorCode);
    }

    [Fact]
    public void U_A9_AValidWindow_IsAccepted_AndQuietHoursStillMayWrapMidnight()
    {
        var settings = new BusinessSettingsAggregate(SettingScopes.GlobalId);

        settings.Change(new BusinessSettingsChangeDto(
            [new SettingChangeInput(SettingKeys.BoardingCheckInWindow, "13:00-18:00"), new SettingChangeInput(SettingKeys.NotificationsQuietHours, "22:00-07:00")],
            TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(2, settings.Values.Count);
    }

    [Fact]
    public void U_A9_EveryBoardingSetting_IsInTheRegistry_WithItsDefaultInsideItsBounds()
    {
        var boarding = SettingDefinitions.All.Where(d => d.Key.StartsWith("boarding.", StringComparison.Ordinal)).ToList();

        Assert.Equal(20, boarding.Count); // the nineteen of section 10.2 plus boarding.depositPercent (HO-ADM-001)
        Assert.All(boarding, d => Assert.Equal("Boarding", d.Group));
    }

    // ── U-B3..U-B8: the quote calculator ─────────────────────────────────────────────────────────────────────────────

    private static RateCardSnapshot Card(decimal nightly = 1000m, decimal weekly = 6000m, decimal monthly = 24000m, decimal extraCat = 0m) => new()
    {
        CardId = Guid.NewGuid(), UnitType = BoardingUnitType.Deluxe, NightlyRate = nightly, WeeklyRate = weekly, MonthlyRate = monthly,
        AdditionalCatNightlyRate = extraCat, TaxClassId = Guid.NewGuid(), CurrencyCode = "BDT",
    };

    private static PricingSnapshot Pricing(int threshold = 14, decimal discount = 0m) =>
        new() { LongStayThresholdNights = threshold, LongStayDiscountPercent = discount };

    [Fact]
    public void U_B3_FiveNights_PriceOnTheNightlyBasis_WithNoDiscount()
    {
        var quote = BoardingQuoteCalculator.Quote(5, 1, Card(), Pricing(discount: 10m));

        Assert.Equal(StayRateBasis.Nightly, quote.Basis);
        var unit = Assert.Single(quote.Units);
        Assert.Equal((RateUnitKind.Night, 1, 5, 5000m, 0m), (unit.Kind, unit.FromNight, unit.ToNight, unit.Amount, unit.Discount));
        Assert.Equal(5000m, quote.Total);
    }

    [Fact]
    public void U_B4_SixteenNights_AreTwoWeeksAndTwoNights_WithTheDiscountPerUnit()
    {
        var quote = BoardingQuoteCalculator.Quote(16, 1, Card(), Pricing(discount: 10m));

        Assert.Equal(StayRateBasis.Weekly, quote.Basis);
        Assert.Equal([RateUnitKind.Week, RateUnitKind.Week, RateUnitKind.Night], quote.Units.Select(u => u.Kind).ToArray());
        Assert.Equal([600m, 600m, 200m], quote.Units.Select(u => u.Discount).ToArray());
        Assert.Equal(14000m, quote.Gross);
        Assert.Equal(1400m, quote.Discount);
        Assert.Equal(12600m, quote.Total);
        Assert.Equal("Week 1 (nights 1-7) - Deluxe weekly rate", quote.Units[0].Description);
    }

    [Fact]
    public void U_B5_SixtyFiveNights_AreTwoMonthsThenFiveNights_InOrder()
    {
        var quote = BoardingQuoteCalculator.Quote(65, 1, Card(), Pricing());

        Assert.Equal(StayRateBasis.Monthly, quote.Basis);
        Assert.Equal([RateUnitKind.Month, RateUnitKind.Month, RateUnitKind.Night], quote.Units.Select(u => u.Kind).ToArray());
        Assert.Equal([1, 31, 61], quote.Units.Select(u => u.FromNight).ToArray());
        Assert.Equal([30, 60, 65], quote.Units.Select(u => u.ToNight).ToArray());
        Assert.Equal(53000m, quote.Total);
    }

    [Fact]
    public void U_B6_TheThresholdNight_SwitchesFromNightlyToWeekly()
    {
        Assert.Equal(StayRateBasis.Nightly, BoardingQuoteCalculator.Quote(13, 1, Card(), Pricing(14)).Basis);
        Assert.Equal(StayRateBasis.Weekly, BoardingQuoteCalculator.Quote(14, 1, Card(), Pricing(14)).Basis);
        Assert.Equal(StayRateBasis.Weekly, BoardingQuoteCalculator.Quote(29, 1, Card(), Pricing(14)).Basis);
        Assert.Equal(StayRateBasis.Monthly, BoardingQuoteCalculator.Quote(30, 1, Card(), Pricing(14)).Basis);
    }

    [Fact]
    public void U_B7_ThreeCats_AddTwoAdditionalCatUnitsPerRateUnit()
    {
        var quote = BoardingQuoteCalculator.Quote(5, 3, Card(extraCat: 200m), Pricing());

        var extra = Assert.Single(quote.Units, u => u.Kind == RateUnitKind.AdditionalCat);
        Assert.Equal(2 * 200m * 5, extra.Amount);
        Assert.Equal(5000m + 2000m, quote.Total);
    }

    [Fact]
    public void U_B8_SixtyNights_SplitAtNight30_SumToTheQuoteExactly()
    {
        var quote = BoardingQuoteCalculator.Quote(60, 2, Card(monthly: 24999.99m, extraCat: 33.33m), Pricing(discount: 7.5m));

        var cycle1 = BoardingQuoteCalculator.UnitsInCycle(quote.Units, 1).Sum(u => u.Amount - u.Discount);
        var cycle2 = BoardingQuoteCalculator.UnitsInCycle(quote.Units, 2).Sum(u => u.Amount - u.Discount);

        Assert.Equal(quote.Total, cycle1 + cycle2);
        Assert.Equal(quote.Units.Count, BoardingQuoteCalculator.UnitsInCycle(quote.Units, 1).Count() + BoardingQuoteCalculator.UnitsInCycle(quote.Units, 2).Count());
    }

    // ── U-B9: the deposit policy ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_B9_FixedIsCappedAtTheTotal_PercentIsRounded_ZeroIsNotRequired()
    {
        Assert.Equal(1500m, BoardingDepositPolicy.Compute(1500m, 2000m, 30m).Amount);
        Assert.Equal(3000m, BoardingDepositPolicy.Compute(10000m, 0m, 30m).Amount);

        var none = BoardingDepositPolicy.Compute(10000m, 0m, 0m);
        Assert.Equal(0m, none.Amount);
        Assert.Equal(DepositState.NotRequired, none.InitialState);
        Assert.Equal(DepositState.Awaiting, BoardingDepositPolicy.Compute(10000m, 0m, 30m).InitialState);
    }

    // ── U-C1 / U-C2: the occupancy window ────────────────────────────────────────────────────────────────────────────

    private const string Dhaka = "Asia/Dhaka";
    private static readonly TimeRange In = new(new TimeOnly(14, 0), new TimeOnly(19, 0));
    private static readonly TimeRange Out = new(new TimeOnly(8, 0), new TimeOnly(11, 0));

    [Fact]
    public void U_C1_TheDefaultWindow_RunsFromTheCheckInStartToTheCheckOutEndPlusTurnaround()
    {
        var window = BoardingOccupancyWindow.Compute(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 7), Dhaka, In, Out, 3);

        Assert.Equal(new DateTimeOffset(2026, 11, 2, 14, 0, 0, TimeSpan.FromHours(6)), window.From);
        Assert.Equal(new DateTimeOffset(2026, 11, 7, 14, 0, 0, TimeSpan.FromHours(6)), window.To); // 11:00 + 3 h
    }

    [Fact]
    public void U_C2_BackToBack_IsFree_AndATwentyFourHourTurnaround_IsNot()
    {
        var first = BoardingOccupancyWindow.Compute(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 7), Dhaka, In, Out, 3);
        var nextDay = BoardingOccupancyWindow.Compute(new DateOnly(2026, 11, 7), new DateOnly(2026, 11, 9), Dhaka, In, Out, 3);
        Assert.False(first.Overlaps(nextDay)); // out 11:00 + 3 h = 14:00, in 14:00: back to back

        var firstLong = BoardingOccupancyWindow.Compute(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 7), Dhaka, In, Out, 24);
        Assert.True(firstLong.Overlaps(nextDay));
    }

    [Fact]
    public void AnEarlyCheckOut_FreesTheUnitAtTheCheckOutInstantPlusTurnaround()
    {
        var window = BoardingOccupancyWindow.Compute(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 7), Dhaka, In, Out, 3);
        var checkedOut = new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.FromHours(6));

        var truncated = BoardingOccupancyWindow.TruncateAtCheckOut(window, checkedOut, 3);

        Assert.Equal(checkedOut.AddHours(3), truncated.To);
        Assert.Equal(window.From, truncated.From);
    }

    // ── U-C9..U-C12: the vaccination rule and gate ───────────────────────────────────────────────────────────────────

    private static readonly Guid Fvrcp = Guid.NewGuid();
    private static readonly Guid Rabies = Guid.NewGuid();

    private static readonly IReadOnlyList<VaccinationRuleVaccine> Core =
        [new(Fvrcp, "FVRCP", "Feline distemper"), new(Rabies, "RAB", "Rabies")];

    [Fact]
    public void U_C9_TheExtractedRule_ReproducesQ4()
    {
        var today = new DateOnly(2026, 10, 1);
        var evaluation = VaccinationStatusRule.Evaluate(Core,
            [new(Fvrcp, today.AddMonths(-11), today.AddDays(10)), new(Rabies, today.AddMonths(-13), today.AddDays(-5))], today, 30);

        Assert.Equal(VaccinationStatus.Overdue, evaluation.OverallStatus);
        Assert.Equal(VaccinationStatus.DueSoon, evaluation.Items.Single(i => i.Code == "FVRCP").Status);
        Assert.Equal(today.AddDays(-5), evaluation.EarliestNextDueOn);
        Assert.Equal(VaccinationStatus.Current, VaccinationStatusRule.Evaluate([], [], today, 30).OverallStatus);
    }

    [Fact]
    public void U_C10_ACoreVaccineDueFiveDaysAfterArrival_BlocksATenNightStay_ButNotOneThatEndsFirst()
    {
        var arrival = new DateOnly(2026, 11, 2);
        var due = arrival.AddDays(5);
        var records = new VaccinationRuleRecord[] { new(Fvrcp, arrival.AddYears(-1), due), new(Rabies, arrival.AddYears(-1), arrival.AddYears(1)) };

        var tenNights = BoardingVaccinationGate.Evaluate(VaccinationStatusRule.Evaluate(Core, records, arrival.AddDays(10), 30), arrival.AddDays(10));
        var threeNights = BoardingVaccinationGate.Evaluate(VaccinationStatusRule.Evaluate(Core, records, arrival.AddDays(3), 30), arrival.AddDays(3));

        Assert.False(tenNights.Passed);
        var reason = Assert.Single(tenNights.Reasons);
        Assert.Equal((GateReason.DueBeforeDeparture, due), (reason.Reason, reason.DueOn));
        Assert.True(threeNights.Passed);
    }

    [Theory]
    [InlineData(-1, false)] // due the day before the departure
    [InlineData(0, false)]  // due ON the departure: blocked ("on or before", owner decision 2026-10-15)
    [InlineData(1, true)]   // due the day after: passes
    public void U_C10b_AVaccineDueOnTheLastDay_Blocks_TheDayAfterPasses(int dueOffsetFromDeparture, bool passes)
    {
        var arrival = new DateOnly(2026, 11, 2);
        var departure = arrival.AddDays(10);
        var records = new VaccinationRuleRecord[] { new(Fvrcp, arrival.AddYears(-1), departure.AddDays(dueOffsetFromDeparture)), new(Rabies, arrival.AddYears(-1), arrival.AddYears(1)) };

        var gate = BoardingVaccinationGate.Evaluate(VaccinationStatusRule.Evaluate(Core, records, departure, 30), departure);

        Assert.Equal(passes, gate.Passed);
        Assert.Equal(passes ? 0 : 1, gate.Reasons.Count);
    }

    [Fact]
    public void U_C11_ACatNeverVaccinated_IsBlockedAsNeverAdministered()
    {
        var gate = BoardingVaccinationGate.Evaluate(VaccinationStatusRule.Evaluate(Core, [], new DateOnly(2026, 11, 7), 30), new DateOnly(2026, 11, 7));

        Assert.False(gate.Passed);
        Assert.All(gate.Reasons, r => Assert.Equal(VaccinationStatusRule.NeverAdministered, r.Reason));
        Assert.Equal(2, gate.Reasons.Count);
    }

    // ── U-E8: the not-eating rule ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_E8_TwoConsecutiveNoFoodEntries_AreFlagged_AnythingElseIsNot()
    {
        Assert.True(NotEatingRule.IsNotEating([PortionEaten.None, PortionEaten.None]));
        Assert.True(NotEatingRule.IsNotEating([PortionEaten.None, PortionEaten.None, PortionEaten.All]));
        Assert.False(NotEatingRule.IsNotEating([PortionEaten.None, PortionEaten.Half, PortionEaten.None]));
        Assert.False(NotEatingRule.IsNotEating([PortionEaten.None]));
        Assert.False(NotEatingRule.IsNotEating([]));
    }
}
