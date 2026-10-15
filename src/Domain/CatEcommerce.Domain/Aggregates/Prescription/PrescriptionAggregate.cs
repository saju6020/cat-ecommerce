using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Prescription;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Prescription;

/// <summary>
/// HO-PHM-001 §8.3 / §8.4 / §8.8 (ADR-142, ADR-148) -- a vet's prescription for a cat, linked to the visit it was written at. Owns its safety record
/// (what the vet saw and acknowledged), the warnings shown, and the owner's repeat requests.
///
/// The status machine is Appendix C's -- <c>Issued -> Dispensed -> Completed</c>, <c>Expired</c>, <c>Cancelled</c> -- with repeats counted as supplies;
/// <c>ValidUntil</c> is the last day any supply may be dispensed. "Active" is derived, never stored (<see cref="PrescriptionActivityRules"/>), and every
/// command treats a passed <c>ValidUntil</c> as expired whether or not the sweep has run. Every rule that can be decided from the prescription's own
/// state is here (rule 20): the cross-aggregate facts (visit open, cat alive, role) are the handler's.
/// </summary>
public sealed class PrescriptionAggregate : AggregateRoot
{
    private readonly List<PrescriptionWarning> _warnings = [];
    private readonly List<RepeatRequest> _repeatRequests = [];

    public string PrescriptionNumber { get; private set; } = default!;
    public Guid BranchId { get; private set; }
    public Guid CatId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VisitId { get; private set; }
    public bool IsRemoteRenewal { get; private set; }
    public Guid PrescriberId { get; private set; }
    public Guid MedicineId { get; private set; }
    public MedicineSnapshot MedicineSnapshot { get; private set; } = default!;
    public Dose Dose { get; private set; } = default!;
    public AdministrationRoute Route { get; private set; } = default!;
    public DoseFrequency Frequency { get; private set; } = default!;
    public int DurationDays { get; private set; }
    public decimal Quantity { get; private set; }
    public string Instructions { get; private set; } = default!;
    public DateTimeOffset IssuedOn { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public bool IsRepeatable { get; private set; }
    public int RepeatsAuthorised { get; private set; }
    public DateOnly ValidUntil { get; private set; }
    public int SuppliesDispensed { get; private set; }
    public DateOnly? LastDispensedOn { get; private set; }
    public PrescriptionStatus Status { get; private set; } = PrescriptionStatus.Issued;
    public DateTimeOffset? CancelledOn { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public string? CancellationReason { get; private set; }
    public Guid? RenewedFromPrescriptionId { get; private set; }
    public Guid? RenewedByPrescriptionId { get; private set; }
    public SafetyRecord SafetyRecord { get; private set; } = default!;

    public IReadOnlyList<PrescriptionWarning> Warnings => _warnings;
    public IReadOnlyList<RepeatRequest> RepeatRequests => _repeatRequests;

    public int SuppliesLeft => PrescriptionActivityRules.SuppliesLeft(RepeatsAuthorised, SuppliesDispensed);
    public DateOnly CourseEndsOn => PrescriptionActivityRules.CourseEndsOn(StartsOn, LastDispensedOn, DurationDays);
    public RepeatRequest? OpenRepeatRequest => _repeatRequests.FirstOrDefault(r => r.IsOpen);

    private PrescriptionAggregate() { }

    public PrescriptionAggregate(Guid id) : base(id) { }

    // ── issue ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    public void Issue(PrescriptionIssueDto dto)
    {
        // The weight requirement has no acknowledgement path (US-PHM-02 AC2); the handler words it with the cat's name before it gets here.
        if (!dto.Assessment.Weight.IsSatisfied)
            throw Violation(BusinessViolationCodes.RecentWeightRequired, "Record the cat's weight before prescribing.", "Weight");

        switch (SafetyAcknowledgement.Evaluate(dto.Assessment, dto.Acknowledgement))
        {
            case AcknowledgementResult.NotAcknowledged:
                throw Violation(BusinessViolationCodes.PrescriptionWarningsNotAcknowledged, "The warnings must be acknowledged before this can be prescribed.", nameof(dto.Acknowledgement));
            case AcknowledgementResult.Changed:
                throw Violation(BusinessViolationCodes.PrescriptionWarningsChanged, "The warnings have changed since they were shown. Review them again.", nameof(dto.Acknowledgement));
            case AcknowledgementResult.AllergyReasonRequired:
                throw Violation(BusinessViolationCodes.AllergyAcknowledgementReasonRequired, "Explain why a recorded allergy is being overridden.", nameof(dto.Acknowledgement));
        }

        if (dto.DurationDays < PrescriptionRules.MinDurationDays || dto.DurationDays > PrescriptionRules.MaxDurationDays)
            throw Violation(BusinessViolationCodes.InvalidDuration, "The duration is between 1 and 365 days.", nameof(dto.DurationDays));

        if (dto.Quantity <= 0m || decimal.Round(dto.Quantity, 2) != dto.Quantity)
            throw Violation(BusinessViolationCodes.InvalidQuantity, "The quantity must be greater than zero with at most two decimal places.", nameof(dto.Quantity));

        var instructions = dto.Instructions?.Trim() ?? "";
        if (instructions.Length is 0 or > PrescriptionRules.MaxInstructionsLength)
            throw Violation(BusinessViolationCodes.InvalidInstructions, "Instructions of 1 to 1000 characters are required.", nameof(dto.Instructions));

        if (!dto.Medicine.AllowedRoutes.Contains(dto.Route))
            throw Violation(BusinessViolationCodes.RouteNotAllowedForMedicine, "That route is not allowed for this medicine.", nameof(dto.Route));

        if (dto.StartsOn < dto.Today.AddDays(-PrescriptionRules.MaxBackdatedStartDays))
            throw Violation(BusinessViolationCodes.InvalidValidity, "A prescription cannot start more than a day in the past.", nameof(dto.StartsOn));

        var validUntil = ResolveValidity(dto);

        PrescriptionNumber = dto.PrescriptionNumber;
        BranchId = dto.BranchId;
        CatId = dto.CatId;
        CustomerId = dto.CustomerId;
        VisitId = dto.VisitId;
        IsRemoteRenewal = dto.IsRemoteRenewal;
        PrescriberId = dto.PrescriberId;
        MedicineId = dto.Medicine.MedicineId;
        MedicineSnapshot = dto.Medicine.ToSnapshot();
        Dose = dto.Dose;
        Route = dto.Route;
        Frequency = dto.Frequency;
        DurationDays = dto.DurationDays;
        Quantity = dto.Quantity;
        Instructions = instructions;
        IssuedOn = dto.Now;
        StartsOn = dto.StartsOn;
        IsRepeatable = dto.IsRepeatable;
        RepeatsAuthorised = dto.IsRepeatable ? dto.RepeatsAuthorised : 0;
        ValidUntil = validUntil;
        SuppliesDispensed = 0;
        LastDispensedOn = null;
        Status = PrescriptionStatus.Issued;
        RenewedFromPrescriptionId = dto.RenewedFromPrescriptionId;

        var assessment = dto.Assessment;
        var acknowledged = assessment.RequiresAcknowledgement;
        SafetyRecord = new SafetyRecord(
            assessment.Weight.LatestKilograms, assessment.Weight.RecordedOn, assessment.DoseCheck.DoseMgPerKg, assessment.DoseCheck.ExpectedMinMgPerKg,
            assessment.DoseCheck.ExpectedMaxMgPerKg, assessment.DoseCheck.Outcome, assessment.Fingerprint,
            acknowledged ? dto.Actor.UserId : null, acknowledged ? dto.Now : null,
            assessment.HasAllergyConflict ? Truncate(dto.Acknowledgement.AllergyReason) : null,
            assessment.Warnings.Any(w => w.RequiresAcknowledgement && (w.Kind == SafetyWarningKind.DoseAboveRange || w.Kind == SafetyWarningKind.DoseBelowRange))
                ? Truncate(dto.Acknowledgement.DoseNote)
                : null);

        _warnings.Clear();
        _warnings.AddRange(assessment.Warnings.Take(PrescriptionRules.MaxWarnings).Select(w =>
            new PrescriptionWarning(Guid.NewGuid(), w.Kind, w.Blocking, w.RequiresAcknowledgement, w.Severity, w.Subject, w.Detail)));

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new PrescriptionIssuedEvent
        {
            PrescriptionId = Id, CatId = CatId, CustomerId = CustomerId, VisitId = VisitId, MedicineId = MedicineId,
            IsControlled = MedicineSnapshot.IsControlled, HadAcknowledgedWarnings = acknowledged,
            CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext
        });
        UpdateVersion();
    }

    private static DateOnly ResolveValidity(PrescriptionIssueDto dto)
    {
        if (dto.IsRepeatable)
        {
            if (dto.RepeatsAuthorised < 1 || dto.RepeatsAuthorised > PrescriptionRules.MaxRepeats)
                throw Violation(BusinessViolationCodes.InvalidValidity, $"A repeatable prescription authorises 1 to {PrescriptionRules.MaxRepeats} repeats.", nameof(dto.RepeatsAuthorised));

            if (dto.ValidUntil is null)
                throw Violation(BusinessViolationCodes.RepeatsRequireValidity, "A repeatable prescription needs the date its repeats stop.", nameof(dto.ValidUntil));

            var latest = dto.StartsOn.AddDays(dto.MaxRepeatValidityDays);
            if (dto.ValidUntil.Value <= dto.StartsOn || dto.ValidUntil.Value > latest)
                throw Violation(BusinessViolationCodes.InvalidValidity, $"Repeats must stop after the start date and within {dto.MaxRepeatValidityDays} days of it.", nameof(dto.ValidUntil));

            return dto.ValidUntil.Value;
        }

        if (dto.RepeatsAuthorised != 0)
            throw Violation(BusinessViolationCodes.InvalidValidity, "A prescription that is not repeatable authorises no repeats.", nameof(dto.RepeatsAuthorised));

        var defaultEnd = dto.StartsOn.AddDays(dto.DefaultDispenseWindowDays);
        if (dto.ValidUntil is null) return defaultEnd;

        if (dto.ValidUntil.Value < dto.StartsOn || dto.ValidUntil.Value > defaultEnd)
            throw Violation(BusinessViolationCodes.InvalidValidity, $"The last dispensing day is between the start date and {dto.DefaultDispenseWindowDays} days after it.", nameof(dto.ValidUntil));

        return dto.ValidUntil.Value;
    }

    // ── supply ──────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The preconditions of <see cref="RecordSupply"/>, callable on their own so a handler refuses BEFORE it asks Inventory to move anything.</summary>
    public void EnsureSupplyAllowed(DateOnly localDate)
    {
        if (Status == PrescriptionStatus.Cancelled)
            throw Transition(PrescriptionActions.Dispense);

        if (SuppliesLeft <= 0)
            throw Violation(BusinessViolationCodes.NoSuppliesRemaining, "Every supply of this prescription has been dispensed.", nameof(SuppliesDispensed));

        if (localDate > ValidUntil)
            throw Violation(BusinessViolationCodes.PrescriptionValidityEnded, PharmacyMessages.PrescriptionValidityEnded(ValidUntil), nameof(ValidUntil));

        if (!PrescriptionActionRules.IsAllowed(Status, PrescriptionActions.Dispense))
            throw Transition(PrescriptionActions.Dispense);
    }

    public void RecordSupply(Guid dispensingId, DateOnly localDate, Guid? repeatRequestId, Guid by, DateTimeOffset now)
    {
        EnsureSupplyAllowed(localDate);

        if (repeatRequestId is { } requestId)
        {
            var request = _repeatRequests.FirstOrDefault(r => r.Id == requestId);
            if (request is null || !request.IsOpen)
                throw Violation(BusinessViolationCodes.RepeatRequestNotFound, "That repeat request is not open on this prescription.", nameof(repeatRequestId));

            request.MarkDispensed(dispensingId, by, now);
        }

        SuppliesDispensed++;
        LastDispensedOn = localDate;
        Status = SuppliesLeft <= 0 ? PrescriptionStatus.Completed : PrescriptionStatus.Dispensed;
        UpdateVersion();
    }

    /// <summary>Only the latest supply may be reversed. <paramref name="previousDispensedOn"/> is the date of the supply before it (null when it was the first).</summary>
    public void ReverseSupply(int supplyNumber, DateOnly? previousDispensedOn)
    {
        if (!PrescriptionActionRules.IsAllowed(Status, PrescriptionActions.ReverseSupply))
            throw Transition(PrescriptionActions.ReverseSupply);

        if (supplyNumber != SuppliesDispensed)
            throw Violation(BusinessViolationCodes.DispensingNotReversible, "Only the latest supply of a prescription can be reversed.", nameof(supplyNumber));

        SuppliesDispensed--;
        LastDispensedOn = previousDispensedOn;
        Status = SuppliesDispensed == 0 ? PrescriptionStatus.Issued : PrescriptionStatus.Dispensed;
        UpdateVersion();
    }

    // ── cancel / expire / renew ─────────────────────────────────────────────────────────────────────────────────────────────────

    public void Cancel(Guid by, string reason, DateTimeOffset now, PharmacyActor actor)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > PrescriptionRules.MaxReasonLength)
            throw Violation(BusinessViolationCodes.CancellationReasonRequired, "Give a reason of at most 500 characters.", nameof(reason));

        if (!PrescriptionActionRules.IsAllowed(Status, PrescriptionActions.Cancel))
            throw Transition(PrescriptionActions.Cancel);

        var trimmed = reason.Trim();
        foreach (var open in _repeatRequests.Where(r => r.IsOpen).ToList())
            open.Decline(by, trimmed, now);

        Status = PrescriptionStatus.Cancelled;
        CancelledOn = now;
        CancelledBy = by;
        CancellationReason = trimmed;

        AddDomainEvent(new PrescriptionCancelledEvent { PrescriptionId = Id, CatId = CatId, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    /// <summary>Whether the sweep should expire it: still issued or dispensed, supplies left, and <c>today</c> is after <c>ValidUntil</c>.</summary>
    public bool CanExpire(DateOnly today) =>
        PrescriptionActionRules.IsAllowed(Status, PrescriptionActions.Expire) && today > ValidUntil && SuppliesLeft > 0;

    public void Expire(DateOnly today, PharmacyActor actor)
    {
        if (!CanExpire(today)) throw Transition(PrescriptionActions.Expire);

        Status = PrescriptionStatus.Expired;
        AddDomainEvent(new PrescriptionExpiredEvent { PrescriptionId = Id, CatId = CatId, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    /// <summary>ADR-148 -- the old prescription points at its renewal. Does not cancel it; the handler calls <see cref="Cancel"/> for one still running.</summary>
    public void MarkRenewed(Guid newPrescriptionId)
    {
        if (RenewedByPrescriptionId is not null)
            throw Violation(BusinessViolationCodes.PrescriptionAlreadyRenewed, "This prescription has already been renewed.", nameof(RenewedByPrescriptionId));

        if (!PrescriptionActionRules.IsAllowed(Status, PrescriptionActions.Renew))
            throw Transition(PrescriptionActions.Renew);

        RenewedByPrescriptionId = newPrescriptionId;
        UpdateVersion();
    }

    // ── repeats (US-PHM-06) ─────────────────────────────────────────────────────────────────────────────────────────────────────

    public RepeatEligibility RepeatEligibilityOn(DateOnly today) =>
        PrescriptionActionRules.EvaluateRepeat(IsRepeatable, Status, SuppliesLeft, ValidUntil, OpenRepeatRequest is not null, today);

    public void RequestRepeat(RepeatRequestDto dto)
    {
        var eligibility = RepeatEligibilityOn(dto.Today);
        if (!eligibility.CanRequest)
            throw Violation(eligibility.ViolationCode!, eligibility.Message!, nameof(dto.RequestId));

        if (_repeatRequests.Count >= RepeatRules.MaxRequestsPerPrescription)
            throw Violation(BusinessViolationCodes.RepeatRequestAlreadyOpen, "This prescription has had too many repeat requests.", nameof(dto.RequestId));

        var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        if (note is { Length: > RepeatRules.MaxNoteLength })
            throw Violation(BusinessViolationCodes.InvalidInstructions, "A note is at most 500 characters.", nameof(dto.Note));

        _repeatRequests.Add(new RepeatRequest(dto.RequestId, dto.RequestedBy, dto.Now, dto.PreferredBranchId, note));

        AddDomainEvent(new RepeatRequestedEvent
        {
            PrescriptionId = Id, RepeatRequestId = dto.RequestId, CatId = CatId, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext
        });
        UpdateVersion();
    }

    public void WithdrawRepeatRequest(Guid requestId, Guid by, DateTimeOffset now)
    {
        var request = _repeatRequests.FirstOrDefault(r => r.Id == requestId && r.RequestedBy == by && r.IsOpen)
            ?? throw Violation(BusinessViolationCodes.RepeatRequestNotFound, "That repeat request is not open.", nameof(requestId));

        request.Withdraw(now);
        UpdateVersion();
    }

    public void DeclineRepeatRequest(Guid requestId, Guid by, string reason, DateTimeOffset now, PharmacyActor actor)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > RepeatRules.MaxDeclineReasonLength)
            throw Violation(BusinessViolationCodes.DeclineReasonRequired, "Give a reason of at most 500 characters.", nameof(reason));

        var request = _repeatRequests.FirstOrDefault(r => r.Id == requestId && r.IsOpen)
            ?? throw Violation(BusinessViolationCodes.RepeatRequestNotFound, "That repeat request is not open.", nameof(requestId));

        request.Decline(by, reason.Trim(), now);

        AddDomainEvent(new RepeatRequestDeclinedEvent
        {
            PrescriptionId = Id, RepeatRequestId = requestId, CatId = CatId, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext
        });
        UpdateVersion();
    }

    // ── merge (ADR-152) ─────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A customer merge moves the prescription to the survivor. Nothing else about it changes.</summary>
    public void ReassignCustomer(Guid survivorId)
    {
        if (CustomerId == survivorId) return;
        CustomerId = survivorId;
        UpdateVersion();
    }

    private static string? Truncate(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().Length <= PrescriptionRules.MaxNoteLength ? text.Trim() : text.Trim()[..PrescriptionRules.MaxNoteLength];

    private DomainRuleViolationException Transition(string action) =>
        Violation(BusinessViolationCodes.InvalidPrescriptionTransition, PharmacyMessages.InvalidTransition(Status.DisplayName, ActionTarget(action)), nameof(Status));

    private static string ActionTarget(string action) => action switch
    {
        PrescriptionActions.Dispense => PrescriptionStatus.Dispensed.DisplayName,
        PrescriptionActions.Cancel => PrescriptionStatus.Cancelled.DisplayName,
        PrescriptionActions.Expire => PrescriptionStatus.Expired.DisplayName,
        PrescriptionActions.Renew => "Renewed",
        PrescriptionActions.ReverseSupply => PrescriptionStatus.Dispensed.DisplayName,
        _ => action
    };

    private static DomainRuleViolationException Violation(string code, string message, string property) => new(code, message, property);
}
