using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Boarding;

/// <summary>
/// HO-BRD-001 section 8.3-8.7 (ADR-217/218/219, renumbered ADR-350/351/352): the boarding reservation of one household, from enquiry to check-out. One aggregate per
/// command, no exception in this slice. Bounded children only (cats, allocations, rate units, status history, and from Phase G the charge lines, referrals and
/// vet commitments); everything that grows with days times events -- care tasks, observations, incidents, media, isolation entries -- is its own root.
///
/// <b>Occupancy.</b> A unit and a cat are held through exclusion constraints over <c>IsActive</c> children (F31): every transition here keeps the allocation and
/// stay-cat <c>IsActive</c> flags equal to <see cref="StayTransitionRules.HoldsOccupancy"/>. A read-then-write check is never the control (ADR-219).
/// </summary>
public sealed partial class BoardingStayAggregate : AggregateRoot
{
    private readonly List<StayCat> _cats = [];
    private readonly List<UnitAllocation> _allocations = [];
    private readonly List<QuotedRateUnit> _rateUnits = [];
    private readonly List<StayStatusChange> _statusHistory = [];

    public Guid BranchId { get; private set; }
    public Guid CustomerId { get; private set; }
    public BoardingChannel Channel { get; private set; } = default!;
    public BoardingStayStatus Status { get; private set; } = default!;
    public DateOnly ArrivalOn { get; private set; }
    public DateOnly DepartureOn { get; private set; }
    public DateTimeOffset OccupiedFrom { get; private set; }
    public DateTimeOffset OccupiedTo { get; private set; }
    public BoardingUnitType RequestedUnitType { get; private set; } = default!;
    public string CurrencyCode { get; private set; } = default!;

    /// <summary>True while any cat of the stay is suspected infectious (Phase E): such a stay is allocated only to an isolation unit (BR-9).</summary>
    public bool RequiresIsolation { get; private set; }

    public RateCardSnapshot? RateCardSnapshot { get; private set; }
    public PricingSnapshot? PricingSnapshot { get; private set; }
    public StayRateBasis? RateBasis { get; private set; }
    public decimal QuoteTotal { get; private set; }
    public decimal DiscountTotal { get; private set; }

    public decimal DepositRequired { get; private set; }
    public DateTimeOffset? DepositDueBy { get; private set; }
    public DepositState DepositState { get; private set; } = DepositState.NotRequired;

    public DateTimeOffset? ReservedOn { get; private set; }
    public DateTimeOffset? ConfirmedOn { get; private set; }
    public Guid? ConfirmedBy { get; private set; }
    public DateTimeOffset? CheckedInOn { get; private set; }
    public DateTimeOffset? CheckedOutOn { get; private set; }
    public DateTimeOffset? CancelledOn { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public BoardingCancellationReason? CancellationReason { get; private set; }
    public string? CancellationNote { get; private set; }
    public bool DepositForfeited { get; private set; }
    public DateTimeOffset? NoShowRecordedOn { get; private set; }
    public Guid? NoShowRecordedBy { get; private set; }

    public bool NeedsAttention { get; private set; }
    public StayAttentionReason? AttentionReason { get; private set; }
    public DateTimeOffset? AttentionClearedOn { get; private set; }
    public Guid? AttentionClearedBy { get; private set; }
    public string? AttentionClearedNote { get; private set; }

    public IReadOnlyList<StayCat> Cats => _cats;
    public IReadOnlyList<UnitAllocation> Allocations => _allocations;
    public IReadOnlyList<QuotedRateUnit> RateUnits => _rateUnits;
    public IReadOnlyList<StayStatusChange> StatusHistory => _statusHistory;

    public int Nights => StayDateRules.Nights(ArrivalOn, DepartureOn);

    /// <summary>The allocation currently holding a unit (the latest active one), or <c>null</c> for an enquiry.</summary>
    public UnitAllocation? CurrentAllocation => StayAllocationRule.Current(_allocations, a => a.IsActive, a => a.OccupiedFrom);

    private BoardingStayAggregate() { }

    public BoardingStayAggregate(Guid id) : base(id) { }

    // ── Enquiry (US-BRD-02/04/07) ────────────────────────────────────────────────────────────────────────────────────

    public void CreateEnquiry(EnquiryDto dto)
    {
        ValidateRange(dto.ArrivalOn, dto.DepartureOn, dto.Now, dto.Dates);
        ValidateCats(dto.Cats);

        BranchId = dto.BranchId;
        CustomerId = dto.CustomerId;
        Channel = dto.Channel;
        Status = BoardingStayStatus.Enquiry;
        CurrencyCode = dto.CurrencyCode;
        ApplyRange(dto.ArrivalOn, dto.DepartureOn, dto.UnitType, dto.Window);
        ReplaceCats(dto.Cats, dto.Gates, dto.Dates, dto.Now);

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new BoardingEnquiryCreatedEvent
        {
            StayId = Id, BranchId = BranchId, CustomerId = CustomerId, ArrivalOn = ArrivalOn, DepartureOn = DepartureOn,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    /// <summary>An enquiry holds no unit, so its dates, cats and unit type may all change; care profiles already given for a cat that stays are kept (US-BRD-04 AC2).</summary>
    public void ChangeEnquiry(ChangeEnquiryDto dto)
    {
        if (Status != BoardingStayStatus.Enquiry)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, BoardingMessages.InvalidTransition(Status.DisplayName, "changed as an enquiry"), nameof(Status));

        ValidateRange(dto.ArrivalOn, dto.DepartureOn, dto.Now, dto.Dates);
        ValidateCats(dto.Cats);

        ApplyRange(dto.ArrivalOn, dto.DepartureOn, dto.UnitType, dto.Window);
        ReplaceCats(dto.Cats, dto.Gates, dto.Dates, dto.Now);

        AddDomainEvent(new BoardingEnquiryChangedEvent { StayId = Id, ArrivalOn = ArrivalOn, DepartureOn = DepartureOn, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    // ── Care profile (US-BRD-05) ─────────────────────────────────────────────────────────────────────────────────────

    public void SetCareProfile(SetCareProfileDto dto)
    {
        if (Status == BoardingStayStatus.CheckedOut || Status == BoardingStayStatus.Cancelled || Status == BoardingStayStatus.NoShow)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, BoardingMessages.InvalidTransition(Status.DisplayName, "given a care profile"), nameof(Status));

        var cat = FindCat(dto.CatId);
        ValidateProfile(dto.Profile);

        var authorityChanged = cat.HasCareProfile && Status != BoardingStayStatus.Enquiry
            && (cat.VetVisitAuthorised != dto.Profile.VetVisitAuthorised || cat.VetSpendingLimit != dto.Profile.VetSpendingLimit);
        if (authorityChanged && dto.ByStaff)
        {
            if (string.IsNullOrWhiteSpace(dto.VetAuthorityReason) || dto.VetAuthorityReason.Trim().Length > StayRules.MaxReasonLength)
                throw new DomainRuleViolationException(BusinessViolationCodes.VetAuthorityReasonRequired,
                    "A reason is required to change the veterinary authorisation or spending limit of a reserved stay.", nameof(dto.VetAuthorityReason));

            var beforeAuthorised = cat.VetVisitAuthorised;
            var beforeLimit = cat.VetSpendingLimit;
            cat.SetProfile(dto.Profile);
            cat.RecordVetAuthorityChange(dto.VetAuthorityReason.Trim());
            AddDomainEvent(new StayVetAuthorityChangedEvent
            {
                StayId = Id, CatId = cat.CatId, BeforeAuthorised = beforeAuthorised, BeforeLimit = beforeLimit, AfterAuthorised = cat.VetVisitAuthorised, AfterLimit = cat.VetSpendingLimit,
                ByStaff = true, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
            });
        }
        else
        {
            cat.SetProfile(dto.Profile);
        }

        AddDomainEvent(new StayCareProfileSetEvent { StayId = Id, CatId = cat.CatId, Status = Status, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    // ── Vaccination gate override (US-BRD-04 AC3) ────────────────────────────────────────────────────────────────────

    /// <summary>The override covers THIS stay only and survives re-evaluation. The reason is mandatory; who and when are stored; the audit row is written by
    /// <c>AuditVaccinationOverrideHandler</c>.</summary>
    public void OverrideGate(OverrideGateDto dto)
    {
        if (Status == BoardingStayStatus.CheckedOut || Status == BoardingStayStatus.Cancelled || Status == BoardingStayStatus.NoShow)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStayTransition, BoardingMessages.InvalidTransition(Status.DisplayName, "given a vaccination override"), nameof(Status));

        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Trim().Length > StayRules.MaxReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.OverrideReasonRequired, $"A reason is required (at most {StayRules.MaxReasonLength} characters).", nameof(dto.Reason));

        var cat = FindCat(dto.CatId);
        cat.Override(dto.UserContext.UserId, dto.Now, dto.Reason.Trim());

        AddDomainEvent(new VaccinationGateOverriddenEvent { StayId = Id, CatId = cat.CatId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    // ── Reserve (US-BRD-06) ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enquiry -> Reserved. Every cat's care profile must be complete; the gate is re-evaluated and any cat still blocked (and not overridden) refuses the
    /// booking; the unit must be an isolation unit when isolation is required and big enough for the cats. On success the unit and the cats are held (the
    /// exclusion constraints), the rate card and settings are snapshotted, the quote stored, and the deposit and its due time set.
    /// </summary>
    public void Reserve(ReserveDto dto)
    {
        StayTransitionRules.EnsureAllowed(Status, StayTransitionRules.Reserve);

        foreach (var cat in _cats)
        {
            if (dto.CatNames.TryGetValue(cat.CatId, out var name)) cat.Rename(name);
            if (dto.Gates.TryGetValue(cat.CatId, out var gate)) cat.ApplyGate(gate, dto.Today);
        }

        var incomplete = _cats.Where(c => !c.HasCareProfile).Select(c => c.CatName).ToList();
        if (incomplete.Count > 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.CareProfileIncomplete,
                $"The care profile is incomplete for {string.Join(", ", incomplete)}.", "CareProfile");

        var blocked = _cats.Where(c => c.GateStatus == VaccinationGateStatus.Blocked).Select(c => c.CatName).ToList();
        if (blocked.Count > 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.VaccinationGateBlocked,
                $"{string.Join(", ", blocked)} {(blocked.Count == 1 ? "has" : "have")} a core vaccination due before departure. Record the vaccination, or ask an administrator to override.", "CatIds");

        if (RequiresIsolation && dto.Unit.Type != BoardingUnitType.Isolation)
            throw new DomainRuleViolationException(BusinessViolationCodes.IsolationUnitRequired, BoardingMessages.IsolationUnitRequired, "UnitId");

        if (dto.Unit.CapacityCats < _cats.Count)
            throw new DomainRuleViolationException(BusinessViolationCodes.UnitCapacityExceeded,
                BoardingMessages.CapacityExceeded(dto.Unit.Type.DisplayName, dto.Unit.CapacityCats, _cats.Count), "CatIds");

        OccupiedFrom = dto.Window.From;
        OccupiedTo = dto.Window.To;
        CurrencyCode = dto.CurrencyCode;
        RateCardSnapshot = dto.Card;
        PricingSnapshot = dto.Pricing;
        RateBasis = dto.Quote.Basis;
        _rateUnits.Clear();
        _rateUnits.AddRange(dto.Quote.Units);
        QuoteTotal = dto.Quote.Total;
        DiscountTotal = dto.Quote.Discount;

        DepositRequired = dto.Deposit.Amount;
        DepositState = dto.Deposit.InitialState;
        DepositDueBy = dto.Deposit.InitialState == DepositState.Awaiting ? dto.Now.AddHours(dto.HoldHours) : null;
        ReservedOn = dto.Now;

        _allocations.Add(new UnitAllocation(Guid.NewGuid(), dto.Unit.UnitId, dto.Unit.Type, dto.Window.From, dto.Window.To, isActive: false, reason: null, dto.UserContext.UserId, dto.Now));

        Transition(StayTransitionRules.Reserve, dto.Now, dto.UserContext.UserId);

        AddDomainEvent(new BoardingStayReservedEvent
        {
            StayId = Id, BranchId = BranchId, CustomerId = CustomerId, UnitId = dto.Unit.UnitId, ArrivalOn = ArrivalOn, DepartureOn = DepartureOn,
            DepositRequired = DepositRequired, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────────

    public StayCat FindCat(Guid catId) =>
        _cats.FirstOrDefault(c => c.CatId == catId)
        ?? throw new DomainRuleViolationException(BusinessViolationCodes.InvalidCareProfile, "That cat is not on this booking.", "CatId");

    /// <summary>Moves to the status the action leads to, records the change, and re-derives every <c>IsActive</c> flag (section 8.4).</summary>
    private void Transition(string action, DateTimeOffset now, Guid by)
    {
        var from = Status;
        var transition = StayTransitionRules.Find(action);
        Status = transition.To;
        _statusHistory.Add(new StayStatusChange(Guid.NewGuid(), from, Status, now, by, _statusHistory.Count + 1));
        SyncOccupancyFlags();
    }

    private void SyncOccupancyFlags()
    {
        var holds = StayTransitionRules.HoldsOccupancy(Status);
        foreach (var allocation in _allocations) allocation.SetActive(holds);
        foreach (var cat in _cats) cat.SetOccupancy(new OccupancyRange(OccupiedFrom, OccupiedTo), holds);
    }

    private void ApplyRange(DateOnly arrivalOn, DateOnly departureOn, BoardingUnitType unitType, OccupancyRange window)
    {
        ArrivalOn = arrivalOn;
        DepartureOn = departureOn;
        RequestedUnitType = unitType;
        OccupiedFrom = window.From;
        OccupiedTo = window.To;
    }

    private static void ValidateRange(DateOnly arrivalOn, DateOnly departureOn, DateTimeOffset now, StayDateContext dates)
    {
        var violation = StayDateRules.Validate(arrivalOn, departureOn, now, dates.TimeZoneId, dates.CheckInWindow, dates.MaxStayNights, dates.BookingHorizonDays, dates.IsOnline);
        if (violation is not null) throw new DomainRuleViolationException(violation.Code, violation.Message, violation.Property);
    }

    private static void ValidateCats(IReadOnlyList<StayCatInput> cats)
    {
        if (cats.Count < 1 || cats.Count > StayRules.MaxCatsPerStay)
            throw new DomainRuleViolationException(BusinessViolationCodes.CatNotBoardable, $"A booking has between 1 and {StayRules.MaxCatsPerStay} cats.", "CatIds");

        if (cats.Select(c => c.CatId).Distinct().Count() != cats.Count)
            throw new DomainRuleViolationException(BusinessViolationCodes.DuplicateCatInStay, "A cat can be on a booking only once.", "CatIds");
    }

    /// <summary>Keeps the care profile (and any override) of a cat that is still on the booking; new cats start without one. Gates are always re-stored.</summary>
    private void ReplaceCats(IReadOnlyList<StayCatInput> inputs, IReadOnlyDictionary<Guid, GateEvaluation> gates, StayDateContext dates, DateTimeOffset now)
    {
        _cats.RemoveAll(existing => inputs.All(i => i.CatId != existing.CatId));

        var today = BoardingOccupancyWindow.LocalDateOf(now, dates.TimeZoneId);
        foreach (var input in inputs)
        {
            var cat = _cats.FirstOrDefault(c => c.CatId == input.CatId);
            if (cat is null)
            {
                cat = new StayCat(Guid.NewGuid(), input.CatId, input.CatName);
                _cats.Add(cat);
            }
            else
            {
                cat.Rename(input.CatName);
            }

            if (gates.TryGetValue(input.CatId, out var gate)) cat.ApplyGate(gate, today);
        }

        SyncOccupancyFlags();
    }

    private void ValidateProfile(CareProfile profile)
    {
        void Fail(string message, string property) => throw new DomainRuleViolationException(BusinessViolationCodes.InvalidCareProfile, message, property);

        if (profile.Feedings.Count < 1 || profile.Feedings.Count > CareProfileRules.MaxFeedings)
            Fail($"A care profile has between 1 and {CareProfileRules.MaxFeedings} feedings.", "Feedings");
        if (profile.Feedings.Select(f => f.Time).Distinct().Count() != profile.Feedings.Count)
            Fail("Two feedings cannot be at the same time.", "Feedings");
        foreach (var feeding in profile.Feedings)
        {
            if (string.IsNullOrWhiteSpace(feeding.Portion) || feeding.Portion.Length > CareProfileRules.MaxPortionLength) Fail("Each feeding needs a portion (up to 100 characters).", "Feedings");
            if (string.IsNullOrWhiteSpace(feeding.FoodDescription) || feeding.FoodDescription.Length > CareProfileRules.MaxFoodDescriptionLength) Fail("Each feeding needs a food description (up to 200 characters).", "Feedings");
        }

        if (profile.Medications.Count > CareProfileRules.MaxMedications)
            Fail($"A care profile has at most {CareProfileRules.MaxMedications} medications.", "Medications");
        foreach (var medication in profile.Medications)
        {
            if (string.IsNullOrWhiteSpace(medication.Name) || medication.Name.Length > CareProfileRules.MaxMedicationNameLength) Fail("Each medication needs a name (up to 100 characters).", "Medications");
            if (string.IsNullOrWhiteSpace(medication.Dose) || medication.Dose.Length > CareProfileRules.MaxDoseLength) Fail("Each medication needs a dose (up to 100 characters).", "Medications");
            if (string.IsNullOrWhiteSpace(medication.Route) || medication.Route.Length > CareProfileRules.MaxRouteLength) Fail("Each medication needs a route (up to 50 characters).", "Medications");
            if (medication.Times.Count < 1 || medication.Times.Count > CareProfileRules.MaxMedicationTimes) Fail($"Each medication needs between 1 and {CareProfileRules.MaxMedicationTimes} dose times.", "Medications");
            if (medication.Times.Distinct().Count() != medication.Times.Count) Fail("A medication cannot have the same dose time twice.", "Medications");
            if (medication.Instructions is { Length: > CareProfileRules.MaxInstructionsLength }) Fail("Medication instructions are too long.", "Medications");
            if (medication.FromOn is { } from && medication.ToOn is { } to && from > to) Fail("A medication cannot end before it starts.", "Medications");
            if ((medication.FromOn is { } f && (f < ArrivalOn || f > DepartureOn)) || (medication.ToOn is { } t && (t < ArrivalOn || t > DepartureOn)))
                Fail("A medication must run within the stay.", "Medications");
        }

        if (profile.LitterNote is { Length: > CareProfileRules.MaxLitterNoteLength }) Fail("The litter note is too long.", "LitterNote");
        if (profile.VetSpendingLimit < 0m || profile.VetSpendingLimit > BillingRules.MaxAmount || decimal.Round(profile.VetSpendingLimit, 2) != profile.VetSpendingLimit)
            Fail("The veterinary spending limit cannot be negative and has at most two decimals.", "VetSpendingLimit");

        var contact = profile.EmergencyContact;
        if (string.IsNullOrWhiteSpace(contact.Name) || contact.Name.Length > CareProfileRules.MaxContactNameLength) Fail("An emergency contact name is required (up to 200 characters).", "EmergencyContact");
        if (string.IsNullOrWhiteSpace(contact.Phone) || contact.Phone.Length > CareProfileRules.MaxContactPhoneLength) Fail("An emergency contact phone is required (up to 30 characters).", "EmergencyContact");
        if (contact.Relationship is { Length: > CareProfileRules.MaxContactRelationshipLength }) Fail("The relationship is too long.", "EmergencyContact");
        if (profile.Notes is { Length: > CareProfileRules.MaxNotesLength }) Fail("The notes are too long.", "Notes");
    }
}
