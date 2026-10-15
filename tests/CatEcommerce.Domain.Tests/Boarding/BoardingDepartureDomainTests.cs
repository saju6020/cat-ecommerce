using System.Reflection;
using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Events;
using Xunit;

namespace CatEcommerce.Domain.Tests.Boarding;

/// <summary>HO-BRD-001 Phase G -- U-G1 .. U-G14: referral, the vet spending limit, approvals, surgery, extension, check-out and the charge plan.</summary>
public sealed class BoardingDepartureDomainTests
{
    private const string Dhaka = "Asia/Dhaka";
    private static readonly UserContext Staff = new(Guid.NewGuid(), Guid.NewGuid());
    private static readonly TimeRange CheckIn = new(new TimeOnly(9, 0), new TimeOnly(18, 0));
    private static readonly TimeRange CheckOut = new(new TimeOnly(8, 0), new TimeOnly(11, 0));
    private static readonly DateOnly Arrival = new(2026, 11, 10);
    private static readonly DateTimeOffset Booked = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid CatId = Guid.NewGuid();
    private static readonly Guid UnitId = Guid.NewGuid();
    private static readonly Guid TaxClassId = Guid.NewGuid();

    private static readonly RateCardSnapshot Card = new()
    {
        CardId = Guid.NewGuid(), UnitType = BoardingUnitType.Standard, NightlyRate = 1000m, WeeklyRate = 6000m, MonthlyRate = 24000m, AdditionalCatNightlyRate = 300m, TaxClassId = TaxClassId, CurrencyCode = "BDT"
    };

    private static readonly PricingSnapshot Pricing = new() { LongStayThresholdNights = 7, LongStayDiscountPercent = 10m, EarlyCheckoutChargedNights = 0, EarlyCheckoutRefundAsCredit = false };

    private static readonly StayDateContext Dates = new(Dhaka, CheckIn, 90, 365, IsOnline: false);

    private static DateTimeOffset At(DateOnly date, int hour) => BoardingOccupancyWindow.InstantOf(date, new TimeOnly(hour, 0), Dhaka);

    private static OccupancyRange WindowOf(DateOnly departure) => BoardingOccupancyWindow.Compute(Arrival, departure, Dhaka, CheckIn, CheckOut, 3);

    private static IReadOnlyDictionary<Guid, GateEvaluation> Pass(DateOnly on) => new Dictionary<Guid, GateEvaluation> { [CatId] = new(true, [], on) };

    private static CareProfile Profile(bool authorised = true, decimal limit = 5000m) => new(
        [new FeedingPlan(new TimeOnly(8, 0), "1/3 cup", FoodSource.OwnFood, "Dry", null)], [], LitterType.Clumping, null, authorised, limit, new EmergencyContact("May", "017", null), null);

    /// <summary>A stay that has been through enquiry, reserve, confirm and check-in (a 5 night Standard stay for one cat).</summary>
    private static BoardingStayAggregate CheckedIn(int nights = 5, bool authorised = true, decimal limit = 5000m)
    {
        var departure = Arrival.AddDays(nights);
        var window = WindowOf(departure);
        var stay = new BoardingStayAggregate(Guid.NewGuid());
        stay.CreateEnquiry(new EnquiryDto(Guid.NewGuid(), Guid.NewGuid(), BoardingChannel.Reception, Arrival, departure, BoardingUnitType.Standard, [new StayCatInput(CatId, "Mitu")], Pass(Arrival), window, Dates, Booked, "BDT", Staff, Guid.NewGuid()));
        stay.SetCareProfile(new SetCareProfileDto(CatId, Profile(authorised, limit), Staff, Guid.NewGuid()));

        var quote = BoardingQuoteCalculator.Quote(nights, 1, Card, Pricing);
        stay.Reserve(new ReserveDto(
            new AllocatedUnit(UnitId, BoardingUnitType.Standard, 1), Card, Pricing, quote, new DepositRequirement(0m, DepositState.NotRequired), Pass(Arrival), window, Booked, new DateOnly(2026, 11, 2), 24, "BDT",
            new Dictionary<Guid, string> { [CatId] = "Mitu" }, Staff, Guid.NewGuid()));
        stay.Confirm(new ConfirmStayDto(0m, Pass(Arrival), new DateOnly(2026, 11, 2), Booked, Staff, Guid.NewGuid()));

        var arrivedAt = At(Arrival, 14);
        stay.CheckIn(new CheckInDto(
            [new CheckInCatDto(CatId, true, true, 4.2m, "Bright", [], true, new SignatureCapture("Owner", SignatureMethod.Typed, null, true, arrivedAt))],
            Pass(Arrival), Arrival, arrivedAt, false, Staff, Guid.NewGuid()));
        return stay;
    }

    private static CheckOutCatDto CheckOutCat(decimal? weight = 4.1m, bool signed = true, IReadOnlyList<BelongingReturnInput>? belongings = null, DateTimeOffset? at = null) =>
        new(CatId, weight, belongings ?? [], signed ? new SignatureCapture("Owner", SignatureMethod.Typed, null, true, at ?? At(Arrival.AddDays(5), 10)) : null);

    private static CheckOutDto CheckOutAt(DateTimeOffset now, DateOnly today, BoardingStayAggregate stay, BoardingQuote? early = null, DateOnly? chargedDeparture = null, CheckOutCatDto? cat = null) =>
        new([cat ?? CheckOutCat(at: now)], today, now, early is null ? new OccupancyRange(stay.OccupiedFrom, stay.OccupiedTo) : BoardingOccupancyWindow.TruncateAtCheckOut(new OccupancyRange(stay.OccupiedFrom, stay.OccupiedTo), now, 3),
            early, chargedDeparture ?? stay.DepartureOn, Staff, Guid.NewGuid());

    // ── referral and the vet spending limit (US-BRD-14) ───────────────────────────────────────────────────────────────

    [Fact]
    public void U_G1_TheHeadroomIsTheLimitPlusApprovedIncreases_LessWhatIsCommitted_AndNeverBelowZero()
    {
        var stay = CheckedIn(limit: 5000m);
        Assert.Equal(5000m, stay.GetHeadroom(CatId).Headroom.Amount);

        stay.RecordVetCommitment(new RecordVetCommitmentDto(Guid.NewGuid(), CatId, 2000m, Booked));
        Assert.Equal(3000m, stay.GetHeadroom(CatId).Headroom.Amount);

        var approval = stay.RequestVetApproval(new RequestVetApprovalDto(CatId, null, 4000m, "Surgery", Booked, Staff, Guid.NewGuid()));
        Assert.Equal(3000m, stay.GetHeadroom(CatId).Headroom.Amount); // a pending request raises nothing
        stay.DecideVetApproval(new DecideVetApprovalDto(approval.Id, true, null, AcknowledgementChannel.Portal, Booked, Staff, Guid.NewGuid()));
        Assert.Equal(9000m, stay.GetHeadroom(CatId).Limit.Amount);
        Assert.Equal(7000m, stay.GetHeadroom(CatId).Headroom.Amount);

        stay.RecordVetCommitment(new RecordVetCommitmentDto(Guid.NewGuid(), CatId, 9000m, Booked));
        Assert.Equal(0m, stay.GetHeadroom(CatId).Headroom.Amount);
    }

    [Fact]
    public void U_G2_ACatWithoutVetAuthorisation_HasOnlyApprovedIncreasesAsItsLimit()
    {
        var stay = CheckedIn(authorised: false, limit: 5000m);

        var result = stay.GetHeadroom(CatId);

        Assert.False(result.VetVisitAuthorised);
        Assert.Equal(0m, result.Limit.Amount);
        Assert.Equal(0m, result.Headroom.Amount);
    }

    [Fact]
    public void U_G3_ACommitmentIsRecordedOncePerCharge()
    {
        var stay = CheckedIn();
        var chargeId = Guid.NewGuid();

        Assert.True(stay.RecordVetCommitment(new RecordVetCommitmentDto(chargeId, CatId, 1000m, Booked)));
        Assert.False(stay.RecordVetCommitment(new RecordVetCommitmentDto(chargeId, CatId, 1000m, Booked)));

        Assert.Single(stay.Commitments);
    }

    [Fact]
    public void U_G4_ACatIsReferredOnlyWhileBoarding_AndNotTwiceAtOnce_AndTheEventNamesTheReferral()
    {
        var stay = CheckedIn();

        var referral = stay.ReferCat(new ReferDto(CatId, "Limping", TriagePriority.Urgent, At(Arrival.AddDays(1), 10), Staff, Guid.NewGuid()));

        var raised = Assert.Single(stay.Events.OfType<BoardedCatReferredToClinicEvent>());
        Assert.Equal(referral.Id, raised.ReferralId);
        Assert.Equal(TriagePriority.Urgent, raised.Urgency);
        Assert.Throws<DomainRuleViolationException>(() => stay.ReferCat(new ReferDto(CatId, "Again", TriagePriority.Routine, At(Arrival.AddDays(1), 11), Staff, Guid.NewGuid())));
        Assert.Throws<DomainRuleViolationException>(() => stay.ReferCat(new ReferDto(CatId, " ", TriagePriority.Routine, At(Arrival.AddDays(1), 11), Staff, Guid.NewGuid())));
    }

    [Fact]
    public void U_G5_ACatAtTheClinic_IsAway_CannotBeCheckedOut_AndIsBackOnReturn()
    {
        var stay = CheckedIn();
        var referral = stay.ReferCat(new ReferDto(CatId, "Limping", TriagePriority.Urgent, At(Arrival.AddDays(1), 10), Staff, Guid.NewGuid()));
        var checkOutAt = At(Arrival.AddDays(5), 10);

        Assert.True(stay.IsAway(CatId, checkOutAt));
        Assert.Equal("At the clinic", stay.AwayReason(CatId, checkOutAt));
        var refused = Assert.Throws<DomainRuleViolationException>(() => stay.CheckOut(CheckOutAt(checkOutAt, Arrival.AddDays(5), stay)));
        Assert.Equal(BusinessViolationCodes.CatNotPresent, refused.ErrorCode);

        stay.ReturnCatFromClinic(referral.Id, At(Arrival.AddDays(2), 9), Staff, Guid.NewGuid());
        Assert.False(stay.IsAway(CatId, checkOutAt));
        var returned = Assert.Single(stay.Events.OfType<CatReturnedFromClinicEvent>());
        Assert.Equal(referral.ReferredOn, returned.AwayFrom);
    }

    [Fact]
    public void U_G6_TheVisitLinkIsStoredOnce_AndAnUnknownReferralIsIgnored()
    {
        var stay = CheckedIn();
        var referral = stay.ReferCat(new ReferDto(CatId, "Limping", TriagePriority.Urgent, At(Arrival.AddDays(1), 10), Staff, Guid.NewGuid()));
        var visitId = Guid.NewGuid();

        Assert.True(stay.LinkReferralVisit(referral.Id, visitId));
        Assert.False(stay.LinkReferralVisit(referral.Id, Guid.NewGuid()));
        Assert.False(stay.LinkReferralVisit(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(visitId, stay.Referrals.Single().VisitId);
    }

    [Fact]
    public void U_G7_AnApprovalIsFinal_AndAPhoneAnswerNeedsANote()
    {
        var stay = CheckedIn();
        var approval = stay.RequestVetApproval(new RequestVetApprovalDto(CatId, null, 3000m, "Dental", Booked, Staff, Guid.NewGuid()));

        Assert.Throws<DomainRuleViolationException>(() => stay.DecideVetApproval(new DecideVetApprovalDto(approval.Id, true, " ", AcknowledgementChannel.Staff, Booked, Staff, Guid.NewGuid())));

        stay.DecideVetApproval(new DecideVetApprovalDto(approval.Id, false, "Owner said no", AcknowledgementChannel.Staff, Booked, Staff, Guid.NewGuid()));
        var again = Assert.Throws<DomainRuleViolationException>(() => stay.DecideVetApproval(new DecideVetApprovalDto(approval.Id, true, null, AcknowledgementChannel.Portal, Booked, Staff, Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.VetApprovalAlreadyDecided, again.ErrorCode);
        Assert.Equal(VetApprovalStatus.Declined, stay.Approvals.Single().Status);
        var decided = stay.Events.OfType<VetChargeApprovalDecidedEvent>().Single();
        Assert.False(decided.Approved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void U_G8_AnApprovalAmount_MustBeAboveZero(int amount)
    {
        var stay = CheckedIn();

        Assert.Throws<DomainRuleViolationException>(() => stay.RequestVetApproval(new RequestVetApprovalDto(CatId, null, amount, "Dental", Booked, Staff, Guid.NewGuid())));
    }

    [Fact]
    public void U_G9_ASurgeryMakesTheCatAwayFromItsStart_UntilDischarge_WithoutReleasingTheUnit()
    {
        var stay = CheckedIn();
        var surgeryId = Guid.NewGuid();
        var starts = At(Arrival.AddDays(2), 9);

        Assert.True(stay.RecordSurgery(surgeryId, CatId, starts, Guid.NewGuid(), Staff));
        Assert.False(stay.RecordSurgery(surgeryId, CatId, starts, Guid.NewGuid(), Staff));
        Assert.False(stay.IsAway(CatId, starts.AddHours(-1)));
        Assert.True(stay.IsAway(CatId, starts.AddHours(1)));
        Assert.Equal("In surgery", stay.AwayReason(CatId, starts.AddHours(1)));
        Assert.NotNull(stay.CurrentAllocation);
        Assert.True(stay.CurrentAllocation!.IsActive);

        Assert.True(stay.DischargeSurgery(surgeryId, starts.AddHours(5), Guid.NewGuid(), Staff));
        Assert.False(stay.DischargeSurgery(surgeryId, starts.AddHours(6), Guid.NewGuid(), Staff));
        Assert.False(stay.IsAway(CatId, starts.AddHours(6)));
        Assert.Equal(starts, stay.Events.OfType<StaySurgeryDischargedEvent>().Single().AwayFrom);
    }

    // ── extension (US-BRD-15) ────────────────────────────────────────────────────────────────────────────────────────

    private static ExtendDto ExtendTo(BoardingStayAggregate stay, DateOnly newDeparture, IReadOnlyDictionary<Guid, GateEvaluation>? gates = null) => new(
        newDeparture, WindowOf(newDeparture), BoardingQuoteCalculator.Quote(StayDateRules.Nights(Arrival, newDeparture), 1, Card, Pricing), gates ?? Pass(newDeparture), 90, Arrival,
        At(Arrival.AddDays(1), 10), Staff, Guid.NewGuid());

    [Fact]
    public void U_G10_AnExtension_GrowsTheAllocationTheCatsAndTheQuote()
    {
        var stay = CheckedIn(nights: 5);
        var before = stay.QuoteTotal;
        var newDeparture = Arrival.AddDays(8);

        stay.Extend(ExtendTo(stay, newDeparture));

        Assert.Equal(newDeparture, stay.DepartureOn);
        Assert.Equal(WindowOf(newDeparture).To, stay.CurrentAllocation!.OccupiedTo);
        Assert.Equal(WindowOf(newDeparture).To, stay.Cats.Single().OccupiedTo);
        Assert.True(stay.QuoteTotal > before);
        Assert.Equal(StayRateBasis.Weekly, stay.RateBasis);
        Assert.Single(stay.Events.OfType<BoardingStayExtendedEvent>());
    }

    [Fact]
    public void U_G11_AnExtension_MustMoveTheDepartureLater_StayWithinTheLimit_AndPassTheGate()
    {
        var stay = CheckedIn(nights: 5);

        Assert.Throws<DomainRuleViolationException>(() => stay.Extend(ExtendTo(stay, Arrival.AddDays(5))));
        Assert.Throws<DomainRuleViolationException>(() => stay.Extend(ExtendTo(stay, Arrival.AddDays(91))));

        var blocked = new Dictionary<Guid, GateEvaluation> { [CatId] = new(false, [new GateReason("FVRCP", "FVRCP", "DueBeforeDeparture", Arrival.AddDays(6))], Arrival) };
        var refused = Assert.Throws<DomainRuleViolationException>(() => stay.Extend(ExtendTo(stay, Arrival.AddDays(8), blocked)));
        Assert.Equal(BusinessViolationCodes.VaccinationGateBlocked, refused.ErrorCode);
        Assert.Equal(Arrival.AddDays(5), stay.DepartureOn);
    }

    // ── check-out (US-BRD-16, US-BRD-17) ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_G12_AnIncompleteCheckOut_NamesEachMissingItemPerCat_AndLeavesTheStayCheckedIn()
    {
        var stay = CheckedIn();
        var now = At(Arrival.AddDays(5), 10);

        var error = Assert.Throws<DomainRuleViolationException>(() => stay.CheckOut(CheckOutAt(now, Arrival.AddDays(5), stay, cat: CheckOutCat(weight: null, signed: false))));

        Assert.Equal(BusinessViolationCodes.CheckOutChecklistIncomplete, error.ErrorCode);
        Assert.Contains("Mitu", error.Message);
        Assert.Contains("weight", error.Message);
        Assert.Contains("owner signature", error.Message);
        Assert.Equal(BoardingStayStatus.CheckedIn, stay.Status);
    }

    [Fact]
    public void U_G13_ABelongingNotReturnedNeedsANote_AndEveryBelongingMustBeAccountedFor()
    {
        var stay = CheckedInWithBelonging(out var belongingId);
        var now = At(Arrival.AddDays(5), 10);

        var missing = Assert.Throws<DomainRuleViolationException>(() => stay.CheckOut(CheckOutAt(now, Arrival.AddDays(5), stay, cat: CheckOutCat(belongings: []))));
        var unexplained = Assert.Throws<DomainRuleViolationException>(() => stay.CheckOut(CheckOutAt(now, Arrival.AddDays(5), stay, cat: CheckOutCat(belongings: [new BelongingReturnInput(belongingId, false, null)]))));
        Assert.Contains("not accounted for", missing.Message);
        Assert.Contains("note", unexplained.Message);

        stay.CheckOut(CheckOutAt(now, Arrival.AddDays(5), stay, cat: CheckOutCat(belongings: [new BelongingReturnInput(belongingId, true, null)])));

        Assert.Equal(BoardingStayStatus.CheckedOut, stay.Status);
        Assert.True(stay.Cats.Single().Belongings.Single().Returned);
        Assert.Equal(4.1m, stay.Events.OfType<StayCatCheckedOutEvent>().Single().WeightKg);
        Assert.False(stay.Events.OfType<BoardingStayCheckedOutEvent>().Single().Early);
    }

    [Fact]
    public void U_G14_AnEarlyCheckOut_ReQuotesForTheChargedNights_AndFreesTheUnitAfterTheTurnaround()
    {
        var stay = CheckedIn(nights: 10);
        var now = At(Arrival.AddDays(3), 10);
        var charged = BoardingCheckoutRules.NightsToCharge(Arrival, stay.DepartureOn, Arrival.AddDays(3), 0);
        var early = BoardingQuoteCalculator.Quote(charged, 1, Card, Pricing);

        stay.CheckOut(CheckOutAt(now, Arrival.AddDays(3), stay, early, Arrival.AddDays(charged)));

        Assert.Equal(3, charged);
        Assert.Equal(Arrival.AddDays(3), stay.DepartureOn);
        Assert.Equal(3000m, stay.QuoteTotal);
        Assert.Equal(StayRateBasis.Nightly, stay.RateBasis);
        Assert.Equal(now.AddHours(3), stay.CurrentAllocation!.OccupiedTo);
        Assert.True(stay.Events.OfType<BoardingStayCheckedOutEvent>().Single().Early);
    }

    private static BoardingStayAggregate CheckedInWithBelonging(out Guid belongingId)
    {
        var departure = Arrival.AddDays(5);
        var window = WindowOf(departure);
        var stay = new BoardingStayAggregate(Guid.NewGuid());
        stay.CreateEnquiry(new EnquiryDto(Guid.NewGuid(), Guid.NewGuid(), BoardingChannel.Reception, Arrival, departure, BoardingUnitType.Standard, [new StayCatInput(CatId, "Mitu")], Pass(Arrival), window, Dates, Booked, "BDT", Staff, Guid.NewGuid()));
        stay.SetCareProfile(new SetCareProfileDto(CatId, Profile(), Staff, Guid.NewGuid()));
        stay.Reserve(new ReserveDto(
            new AllocatedUnit(UnitId, BoardingUnitType.Standard, 1), Card, Pricing, BoardingQuoteCalculator.Quote(5, 1, Card, Pricing), new DepositRequirement(0m, DepositState.NotRequired), Pass(Arrival), window, Booked,
            new DateOnly(2026, 11, 2), 24, "BDT", new Dictionary<Guid, string> { [CatId] = "Mitu" }, Staff, Guid.NewGuid()));
        stay.Confirm(new ConfirmStayDto(0m, Pass(Arrival), new DateOnly(2026, 11, 2), Booked, Staff, Guid.NewGuid()));
        var arrivedAt = At(Arrival, 14);
        stay.CheckIn(new CheckInDto(
            [new CheckInCatDto(CatId, true, true, 4.2m, "Bright", [new BelongingInput(BelongingKind.Bedding, "Blue blanket", [])], false, new SignatureCapture("Owner", SignatureMethod.Typed, null, true, arrivedAt))],
            Pass(Arrival), Arrival, arrivedAt, false, Staff, Guid.NewGuid()));

        belongingId = stay.Cats.Single().Belongings.Single().Id;
        return stay;
    }

    // ── charge planning (section 8.15) ───────────────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<QuotedRateUnit> Units(int nights) => BoardingQuoteCalculator.Quote(nights, 1, Card, Pricing).Units;

    [Fact]
    public void U_G15_TheFirstPlanRecordsEveryUnitUnderRevisionOne_AndASecondPlanChangesNothing()
    {
        var units = Units(5);

        var first = BoardingChargePlanner.Plan([], units, currentRevision: 0);
        Assert.Equal(1, first.Revision);
        Assert.Single(first.Record);
        Assert.StartsWith("r1.", first.Record[0].LineRef);

        var rows = first.Record.Select(c => new BoardingChargeRow(c.LineRef, Guid.NewGuid(), false, false, c.Unit.Net)).ToList();
        var second = BoardingChargePlanner.Plan(rows, units, first.Revision);
        Assert.False(second.HasChanges);
        Assert.Equal(1, second.Revision);
    }

    [Fact]
    public void U_G16_AnExtensionVoidsTheUninvoicedChargeThatChanged_AndRecordsTheNewOnes()
    {
        var oldUnits = Units(5);
        var first = BoardingChargePlanner.Plan([], oldUnits, 0);
        var rows = first.Record.Select(c => new BoardingChargeRow(c.LineRef, Guid.NewGuid(), false, false, c.Unit.Net)).ToList();

        var extended = BoardingChargePlanner.Plan(rows, Units(8), first.Revision);

        Assert.Equal(first.Record.Select(c => c.LineRef), extended.VoidRefs);
        Assert.Equal(2, extended.Revision);
        Assert.All(extended.Record, c => Assert.StartsWith("r2.", c.LineRef));
        Assert.Equal(0m, extended.CreditDue);
    }

    [Fact]
    public void U_G17_AnInvoicedChargeThatStillApplies_IsKept_AndOneThatDoesNot_BecomesACredit()
    {
        var monthUnits = Units(40); // Month 1 (nights 1-30) + a week + nights
        var first = BoardingChargePlanner.Plan([], monthUnits, 0);
        var rows = first.Record.Select(c => new BoardingChargeRow(c.LineRef, Guid.NewGuid(), false, c.Unit.Kind == RateUnitKind.Month, c.Unit.Net)).ToList();

        // Leaving at night 33: the invoiced month still applies; the week (uninvoiced) is replaced by nights.
        var early = BoardingChargePlanner.Plan(rows, Units(33), first.Revision);
        Assert.DoesNotContain(rows.First(r => r.IsBilled).LineRef, early.VoidRefs);
        Assert.Equal(0m, early.CreditDue);
        Assert.Contains(early.Record, c => c.Unit.Kind == RateUnitKind.Night);

        // Leaving at night 10: the invoiced month no longer applies -> a credit of its net.
        var short_ = BoardingChargePlanner.Plan(rows, Units(10), first.Revision);
        Assert.Equal(rows.First(r => r.IsBilled).Net, short_.CreditDue);
    }

    [Theory]
    [InlineData(5, 0, 5)]   // on time
    [InlineData(10, 0, 5)]  // after the booked departure: the booked nights
    [InlineData(3, 0, 3)]   // two nights short
    [InlineData(1, 0, 1)]
    [InlineData(0, 0, 1)]   // same day: at least one night
    [InlineData(3, 2, 5)]   // plus the configured extra nights, never beyond the booked
    [InlineData(1, 9, 5)]
    public void U_G18_EarlyCheckOut_ChargesTheNightsUsed_PlusTheConfiguredExtra_NeverMoreThanBooked(int checkoutDay, int extra, int expected)
    {
        var charged = BoardingCheckoutRules.NightsToCharge(Arrival, Arrival.AddDays(5), Arrival.AddDays(checkoutDay), extra);

        Assert.Equal(expected, charged);
    }

    [Fact]
    public void U_G19_ARelocationOption_IsASameTypeUnitFreeForTheRemainingStay()
    {
        var current = new AvailabilityUnit(UnitId, "S1", BoardingUnitType.Standard, 1, "A", 1, true);
        var free = new AvailabilityUnit(Guid.NewGuid(), "S2", BoardingUnitType.Standard, 1, "A", 2, true);
        var busy = new AvailabilityUnit(Guid.NewGuid(), "S3", BoardingUnitType.Standard, 1, "A", 3, true);
        var otherType = new AvailabilityUnit(Guid.NewGuid(), "D1", BoardingUnitType.Deluxe, 1, "B", 1, true);
        var inactive = new AvailabilityUnit(Guid.NewGuid(), "S4", BoardingUnitType.Standard, 1, "A", 4, false);
        var range = new OccupancyRange(At(Arrival, 12), At(Arrival.AddDays(8), 12));
        var inputs = new BoardingInputSet(
            null!, [current, free, busy, otherType, inactive],
            new Dictionary<Guid, IReadOnlyList<OccupancyRange>> { [busy.UnitId] = [new OccupancyRange(At(Arrival.AddDays(6), 12), At(Arrival.AddDays(9), 12))] }, new Dictionary<int, RateCardSnapshot>());

        var options = RelocationOptions.Find(inputs, BoardingUnitType.Standard, 1, range, UnitId);

        Assert.Equal(["S2"], options.Select(o => o.Code));
    }

    [Fact]
    public void U_G20_NoBoardingEventCarriesAStringProperty()
    {
        var eventTypes = typeof(BoardingStayCheckedOutEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(BoardingStayCheckedOutEvent).Namespace && typeof(Event).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(eventTypes);
        var violations = eventTypes
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        Assert.True(violations.Count == 0, "Boarding events carry ids, instants, dates, enums, booleans and decimals only: " + string.Join(", ", violations));
    }

    [Fact]
    public void U_G21_ClearingAttentionNeedsANote_AndKeepsWhoAndWhen()
    {
        var stay = CheckedIn();
        stay.FlagAttention(StayAttentionReason.CatDeceased);

        Assert.Throws<DomainRuleViolationException>(() => stay.ClearAttention(" ", Booked, Staff.UserId));
        stay.ClearAttention("Spoke to the owner", Booked, Staff.UserId);

        Assert.False(stay.NeedsAttention);
        Assert.Equal("Spoke to the owner", stay.AttentionClearedNote);
        Assert.Equal(Staff.UserId, stay.AttentionClearedBy);
    }

    // ── gate-fix round 2026-10-15 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_G22_AnInvoicedChargeThatNoLongerApplies_IsOwedOnlyWhatIsNotYetCredited()
    {
        var month = Units(40).First(u => u.Kind == RateUnitKind.Month);
        var ref1 = BoardingChargePlanner.RefOf(1, month);
        BoardingChargeRow Billed(decimal credited) => new(ref1, Guid.NewGuid(), false, true, month.Net, credited);

        var untouched = BoardingChargePlanner.Plan([Billed(0m)], Units(10), 1);
        var partly = BoardingChargePlanner.Plan([Billed(month.Net - 100m)], Units(10), 1);
        var cleared = BoardingChargePlanner.Plan([Billed(month.Net)], Units(10), 1);

        Assert.Equal(month.Net, untouched.CreditDue);
        Assert.Equal(100m, partly.CreditDue);
        Assert.Equal(0m, cleared.CreditDue); // the credit note covers it: nothing left to give back
    }

    [Theory]
    [InlineData(0, true, "None")]
    [InlineData(0, false, "None")]
    [InlineData(250, true, "Credit")]
    [InlineData(250, false, "Refund")]
    public void U_G23_TheCreditOutcome_FollowsTheSnapshottedRefundAsCreditSetting(int credit, bool refundAsCredit, string expected)
    {
        Assert.Equal(expected, EarlyCheckoutCredit.Outcome(credit, refundAsCredit));
    }

    [Fact]
    public void U_G24_ACheckOutAfterTheBookedDeparture_ChargesTheExtraNights_AndDoesNotExtendTheAllocation()
    {
        var stay = CheckedIn(nights: 3);
        var checkOutDay = Arrival.AddDays(5); // two days over
        var now = At(checkOutDay, 10);
        var allocationEnd = stay.CurrentAllocation!.OccupiedTo;
        var unit = stay.CurrentAllocation!.UnitId;

        var outcome = EarlyCheckout.Compute(stay, checkOutDay, now, 3);

        Assert.True(outcome.IsLate);
        Assert.False(outcome.IsEarly);
        Assert.Equal(5, outcome.ChargedNights);
        Assert.Equal(5000m, outcome.Quote!.Total);
        Assert.Equal(allocationEnd, outcome.Window.To); // charges only: the allocation is never extended ("Charge extra nights, never block")

        stay.CheckOut(new CheckOutDto([CheckOutCat(at: now)], checkOutDay, now, outcome.Window, outcome.Quote, outcome.ChargedDepartureOn, Staff, Guid.NewGuid()));

        Assert.Equal(Arrival.AddDays(5), stay.DepartureOn);
        Assert.Equal(5000m, stay.QuoteTotal);
        Assert.Equal(allocationEnd, stay.CurrentAllocation!.OccupiedTo);
        var raised = stay.Events.OfType<BoardingStayCheckedOutEvent>().Single();
        Assert.False(raised.Early);
        Assert.True(raised.Late);
        Assert.Equal((unit, allocationEnd, now), (raised.OverstayUnitId, raised.OverstayFrom, raised.OverstayTo));
    }

    [Fact]
    public void U_G25_TheAggregateAndThePlainValueOverloads_GiveTheSameEarlyCheckOutAnswer()
    {
        var stay = CheckedIn(nights: 10);
        var day = Arrival.AddDays(3);
        var now = At(day, 10);
        var input = new EarlyCheckoutInput(stay.Status, stay.ArrivalOn, stay.DepartureOn, stay.Cats.Count, stay.OccupiedFrom, stay.OccupiedTo, stay.RateCardSnapshot, stay.PricingSnapshot);

        var viaAggregate = EarlyCheckout.Compute(stay, day, now, 3);
        var viaValues = EarlyCheckout.Compute(input, day, now, 3);

        Assert.Equal(viaAggregate.IsEarly, viaValues.IsEarly);
        Assert.Equal(viaAggregate.ChargedNights, viaValues.ChargedNights);
        Assert.Equal(viaAggregate.Quote!.Total, viaValues.Quote!.Total);
        Assert.Equal(viaAggregate.Window, viaValues.Window);
    }

    [Fact]
    public void U_G26_ThePlainValueHeadroomAndAwayRules_AgreeWithTheAggregate()
    {
        var stay = CheckedIn(limit: 5000m);
        stay.RecordVetCommitment(new RecordVetCommitmentDto(Guid.NewGuid(), CatId, 1200m, Booked));
        var approval = stay.RequestVetApproval(new RequestVetApprovalDto(CatId, null, 800m, "X-ray", Booked, Staff, Guid.NewGuid()));
        stay.DecideVetApproval(new DecideVetApprovalDto(approval.Id, true, null, AcknowledgementChannel.Portal, Booked, Staff, Guid.NewGuid()));
        var referral = stay.ReferCat(new ReferDto(CatId, "Limping", TriagePriority.Urgent, At(Arrival.AddDays(1), 10), Staff, Guid.NewGuid()));

        var viaAggregate = stay.GetHeadroom(CatId);
        var viaValues = VetSpendingHeadroom.Compute(true, 5000m, [800m], [1200m], "BDT");

        Assert.Equal(viaAggregate.Headroom, viaValues.Headroom);
        Assert.Equal(viaAggregate.Limit, viaValues.Limit);
        Assert.Equal(stay.AwayReason(CatId, At(Arrival.AddDays(2), 9)), StayAwayRule.Reason(CatId, At(Arrival.AddDays(2), 9), [(CatId, true)], []));
        Assert.Equal(referral.IsOpen, true);
    }

    [Fact]
    public void U_G27_StaffChangingAReservedStaysVetAuthority_NeedAReason_AndRaiseTheAuditEvent_TheOwnerDoesNot()
    {
        var stay = CheckedIn(authorised: true, limit: 5000m);

        var refused = Assert.Throws<DomainRuleViolationException>(() =>
            stay.SetCareProfile(new SetCareProfileDto(CatId, Profile(true, 9000m), Staff, Guid.NewGuid(), ByStaff: true, VetAuthorityReason: " ")));
        Assert.Equal(BusinessViolationCodes.VetAuthorityReasonRequired, refused.ErrorCode);
        Assert.Equal(5000m, stay.Cats.Single().VetSpendingLimit);

        stay.SetCareProfile(new SetCareProfileDto(CatId, Profile(true, 9000m), Staff, Guid.NewGuid(), ByStaff: true, VetAuthorityReason: "Owner phoned"));
        var changed = Assert.Single(stay.Events.OfType<StayVetAuthorityChangedEvent>());
        Assert.Equal((5000m, 9000m), (changed.BeforeLimit, changed.AfterLimit));
        Assert.Equal("Owner phoned", stay.Cats.Single().VetAuthorityChangeReason);

        stay.SetCareProfile(new SetCareProfileDto(CatId, Profile(false, 100m), Staff, Guid.NewGuid())); // the owner (not staff) changes their own profile
        Assert.Single(stay.Events.OfType<StayVetAuthorityChangedEvent>());
    }
}
