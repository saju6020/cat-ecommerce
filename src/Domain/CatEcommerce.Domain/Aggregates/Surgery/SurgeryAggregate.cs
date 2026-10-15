using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;
using RecoveryStatusEnum = CatEcommerce.Shared.Common.Enums.RecoveryStatus;

namespace CatEcommerce.Domain.Aggregates.Surgery;

/// <summary>
/// HO-SRG-001 §8.2/§8.3 (ADR-157) -- a surgical procedure on a named cat, from booking to the last suture check. The status machine is Appendix C:
/// <c>Scheduled -> PreOp -> InTheatre -> Recovery -> Discharged</c>, with <c>Cancelled</c> from <c>Scheduled</c> and <c>PreOp</c>; an abandoned procedure is
/// <c>Outcome = Abandoned</c>, not a state. The table lives in <see cref="SurgeryTransitionRules"/>, shared with the read side.
///
/// <b>The invariants that can be checked from this aggregate's own state live here</b> (rules 20): the checklist (BR-5's gate), the override rule, recovery and
/// discharge, follow-up windows. The facts that live elsewhere arrive as inputs the handler has already read -- consent re-read from the consent records, the
/// cat's deceased flag, the branch zone. The schedule is held by an appointment (ADR-158); the handler changes both in one save.
/// The anaesthesia log and the surgical report are their own roots (ADR-162, ADR-165), so a nurse logging vitals never bumps this row's <c>xmin</c>.
/// </summary>
public sealed class SurgeryAggregate : AggregateRoot
{
    private readonly List<PreOpChecklistItem> _checklist = [];
    private readonly List<SurgeryFollowUp> _followUps = [];
    private readonly List<SurgeryStatusChange> _statusHistory = [];
    private readonly List<RecoveryStatusChange> _recoveryHistory = [];

    public Guid BranchId { get; private set; }
    public Guid CatId { get; private set; }
    public Guid CustomerId { get; private set; }

    // Named ClinicServiceId, not ServiceId -- the platform Entity base class already declares ServiceId (CS0108), like AppointmentAggregate.
    public Guid ClinicServiceId { get; private set; }
    public ProcedureSnapshot Procedure { get; private set; } = default!;
    public SurgeryPriceSnapshot PriceSnapshot { get; private set; } = default!;
    public Guid AppointmentId { get; private set; }
    public Guid VisitId { get; private set; }
    public Guid TheatreId { get; private set; }
    public Guid SurgeonId { get; private set; }
    public Guid AssistantId { get; private set; }
    public DateTimeOffset StartsOn { get; private set; }
    public DateTimeOffset EndsOn { get; private set; }

    /// <summary>When the owner is asked to bring the cat in by: <see cref="StartsOn"/> minus <c>surgery.admissionLeadMinutes</c> at the time of booking.</summary>
    public DateTimeOffset AdmitBy { get; private set; }

    /// <summary>The fasting instructions the owner was given, snapshotted at booking from the protocol (ADR-167).</summary>
    public string FastingInstructions { get; private set; } = default!;

    /// <summary><see cref="StartsOn"/> in round-trip format; changed by every reschedule; the pre-op reminder guard compares it.</summary>
    public string ScheduleStamp { get; private set; } = default!;

    public SurgeryStatus Status { get; private set; } = default!;
    public AsaGrade? AsaGrade { get; private set; }
    public bool AsaIsEmergency { get; private set; }
    public ConsentOverride? ConsentOverride { get; private set; }
    public DateTimeOffset? AdmittedOn { get; private set; }
    public DateTimeOffset? ProcedureStartedOn { get; private set; }
    public DateTimeOffset? ProcedureEndedOn { get; private set; }

    /// <summary>The branch-local date the procedure ended on -- the neuter date of a completed sterilisation (US-SRG-06).</summary>
    public DateOnly? ProcedureEndedLocalDate { get; private set; }

    public DateTimeOffset? DischargedOn { get; private set; }

    /// <summary>The cat's owner at the moment of discharge -- the customer HO-BIL-001 Phase J bills (owner decision 2026-10-14, "owner at discharge", the same rule as a
    /// visit's <c>CustomerId</c>). Set once by <see cref="Discharge"/>; null on a surgery discharged before it existed or whose cat record was missing, and Billing then
    /// does not guess. <see cref="CustomerId"/> stays the booking customer.</summary>
    public Guid? OwnerAtDischargeId { get; private set; }
    public ProcedureOutcome? Outcome { get; private set; }
    public RecoveryStatus? RecoveryStatus { get; private set; }
    public string? PostOpInstructions { get; private set; }
    public bool CatWasDeceasedAtDischarge { get; private set; }
    public DateTimeOffset? CancelledOn { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public string? CancellationReason { get; private set; }

    public IReadOnlyList<PreOpChecklistItem> Checklist => _checklist;
    public IReadOnlyList<SurgeryFollowUp> FollowUps => _followUps;
    public IReadOnlyList<SurgeryStatusChange> StatusHistory => _statusHistory;
    public IReadOnlyList<RecoveryStatusChange> RecoveryHistory => _recoveryHistory;

    private SurgeryAggregate() { }

    public SurgeryAggregate(Guid id) : base(id) { }

    // ── §8.3 Schedule ───────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Creates the surgery with its five checklist items, all Outstanding, in checklist order (§8.6). The handler has already refused any clash.</summary>
    public void Schedule(SurgeryScheduleDto dto)
    {
        BranchId = dto.BranchId;
        CatId = dto.CatId;
        CustomerId = dto.CustomerId;
        ClinicServiceId = dto.ServiceId;
        Procedure = dto.Procedure;
        PriceSnapshot = dto.Price;
        AppointmentId = dto.AppointmentId;
        VisitId = dto.VisitId;
        TheatreId = dto.TheatreId;
        SurgeonId = dto.SurgeonId;
        AssistantId = dto.AssistantId;
        StartsOn = dto.StartsOn;
        EndsOn = dto.EndsOn;
        AdmitBy = dto.AdmitBy;
        FastingInstructions = dto.FastingInstructions;
        ScheduleStamp = Stamp(dto.StartsOn);
        Status = SurgeryStatus.Scheduled;

        _checklist.Clear();
        foreach (var kind in PreOpChecklistRules.Order)
            _checklist.Add(new PreOpChecklistItem(Guid.NewGuid(), kind));

        AddHistory(null, SurgeryStatus.Scheduled, dto.Now, dto.UserContext.UserId);
        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new SurgeryScheduledEvent
        {
            SurgeryId = Id, AppointmentId = AppointmentId, BranchId = BranchId, CatId = CatId, CustomerId = CustomerId, ServiceId = ClinicServiceId,
            StartsOn = StartsOn, PricingBasis = PriceSnapshot.Basis, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    public void Reschedule(SurgeryRescheduleDto dto)
    {
        SurgeryTransitionRules.EnsureAllowed(Status, SurgeryTransitionRules.Reschedule);

        var previousStartsOn = StartsOn;
        var startChanged = previousStartsOn != dto.StartsOn;

        TheatreId = dto.TheatreId;
        SurgeonId = dto.SurgeonId;
        AssistantId = dto.AssistantId;
        StartsOn = dto.StartsOn;
        EndsOn = dto.EndsOn;
        AdmitBy = dto.AdmitBy;
        FastingInstructions = dto.FastingInstructions;
        ScheduleStamp = Stamp(dto.StartsOn);

        // Date-bound items (§8.6): the fasting instructions named a time the owner was told; a weight is only good for a window around the surgery date.
        if (startChanged)
        {
            var fasting = ItemOf(PreOpChecklistItemKind.FastingInstructionsIssued);
            if (fasting.Status == ChecklistItemStatus.Done) fasting.Revert();

            var weight = ItemOf(PreOpChecklistItemKind.WeightRecorded);
            if (weight.Status == ChecklistItemStatus.Done
                && (weight.ReferenceDate is not { } recordedOn || !PreOpChecklistRules.IsWeightInWindow(recordedOn, dto.NewLocalDate, dto.WeightValidityDays)))
                weight.Revert();
        }

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new SurgeryRescheduledEvent
        {
            SurgeryId = Id, AppointmentId = AppointmentId, BranchId = BranchId, CatId = CatId, StartsOn = StartsOn, PreviousStartsOn = previousStartsOn,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    public void Cancel(SurgeryCancelDto dto)
    {
        SurgeryTransitionRules.EnsureAllowed(Status, SurgeryTransitionRules.Cancel);

        var reason = dto.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > SurgeryRules.CancellationReasonMax)
            throw new DomainRuleViolationException(BusinessViolationCodes.CancellationReasonRequired,
                $"Give a reason for the cancellation (up to {SurgeryRules.CancellationReasonMax} characters).", nameof(dto.Reason));

        Transition(SurgeryStatus.Cancelled, dto.Now, dto.UserContext.UserId);
        CancelledOn = dto.Now;
        CancelledBy = dto.UserContext.UserId;
        CancellationReason = reason;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new SurgeryCancelledEvent { SurgeryId = Id, BranchId = BranchId, CatId = CatId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    // ── admission, the checklist and the start gate (US-SRG-02, US-SRG-03, BR-5) ──────────────────────────────────────────────────

    /// <summary>On the surgery's local date in the branch zone, or up to <c>surgery.earlyAdmissionHours</c> before <see cref="StartsOn"/> (a cat admitted the
    /// evening before) -- else <c>AdmissionNotToday</c>.</summary>
    public void Admit(SurgeryAdmitDto dto)
    {
        SurgeryTransitionRules.EnsureAllowed(Status, SurgeryTransitionRules.Admit);

        var sameLocalDate = BranchCalendar.LocalDateOf(dto.Now, dto.TimeZoneId) == BranchCalendar.LocalDateOf(StartsOn, dto.TimeZoneId);
        var earlyEnough = dto.Now >= StartsOn.AddHours(-dto.EarlyAdmissionHours);
        if (!sameLocalDate && !earlyEnough)
            throw new DomainRuleViolationException(BusinessViolationCodes.AdmissionNotToday, "This surgery cannot be admitted yet. Admit the cat on the day of surgery.", nameof(StartsOn));

        Transition(SurgeryStatus.PreOp, dto.Now, dto.UserContext.UserId);
        AdmittedOn = dto.Now;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new SurgeryAdmittedEvent
        {
            SurgeryId = Id, VisitId = VisitId, CatId = CatId, CustomerId = CustomerId, BranchId = BranchId, SurgeonId = SurgeonId, AdmittedOn = dto.Now,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    /// <summary>Nurse or vet, by note: fasting instructions issued, pre-anaesthetic blood work reviewed. Weight and consent complete from events; the ASA grade has its own method.</summary>
    public void CompleteChecklistItem(ChecklistCompleteDto dto)
    {
        EnsureChecklistOpen();
        if (dto.Kind == PreOpChecklistItemKind.ConsentSigned || dto.Kind == PreOpChecklistItemKind.AsaGradeAssessed)
            throw new DomainRuleViolationException(BusinessViolationCodes.ChecklistItemNotManual,
                dto.Kind == PreOpChecklistItemKind.ConsentSigned
                    ? "Consent is recorded on the consent form; it completes this item by itself."
                    : "Record the ASA grade instead.", nameof(dto.Kind));

        if (dto.Note is { Length: > SurgeryRules.NoteMax })
            throw new DomainRuleViolationException(BusinessViolationCodes.SurgeryTextInvalid, $"A note is at most {SurgeryRules.NoteMax} characters.", nameof(dto.Note));

        var item = ItemOf(dto.Kind);
        if (item.Status == ChecklistItemStatus.Done)
            throw new DomainRuleViolationException(BusinessViolationCodes.ChecklistItemAlreadyDone, $"The {PreOpChecklistRules.Describe(dto.Kind)} item is already done.", nameof(dto.Kind));

        item.Complete(dto.UserContext.UserId, dto.Now, dto.Note?.Trim(), dto.ReferenceId, dto.ReferenceDate);
        SetDefaultValue(dto.UserContext);
        UpdateVersion();
    }

    /// <summary>Only the blood work is waivable, by a vet, with a reason (young healthy cats). Fasting, weight and ASA grade are not; consent NEVER is (BR-5).</summary>
    public void WaiveChecklistItem(ChecklistWaiveDto dto)
    {
        EnsureChecklistOpen();
        if (dto.Kind != PreOpChecklistItemKind.PreAnaestheticBloodWork || !dto.WaiverIsVet)
            throw new DomainRuleViolationException(BusinessViolationCodes.ChecklistItemNotWaivable,
                dto.Kind == PreOpChecklistItemKind.PreAnaestheticBloodWork
                    ? "Only a veterinarian may waive the pre-anaesthetic blood work."
                    : $"The {PreOpChecklistRules.Describe(dto.Kind)} item cannot be waived.", nameof(dto.Kind));

        var reason = dto.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > SurgeryRules.WaiverReasonMax)
            throw new DomainRuleViolationException(BusinessViolationCodes.ChecklistItemNotWaivable, $"Give a reason for the waiver (up to {SurgeryRules.WaiverReasonMax} characters).", nameof(dto.Reason));

        ItemOf(dto.Kind).Waive(dto.UserContext.UserId, dto.Now, reason);
        SetDefaultValue(dto.UserContext);
        UpdateVersion();
    }

    /// <summary>A vet records the ASA grade (I to V, with the E suffix as a flag); re-recording replaces it while the surgery is still before theatre.</summary>
    public void RecordAsaGrade(AsaGradeDto dto)
    {
        EnsureChecklistOpen();
        if (!dto.CallerIsVet)
            throw new DomainRuleViolationException(BusinessViolationCodes.VeterinarianRequired, "Only a veterinarian records the ASA grade.", nameof(dto.Grade));

        AsaGrade = dto.Grade;
        AsaIsEmergency = dto.IsEmergency;
        ItemOf(PreOpChecklistItemKind.AsaGradeAssessed).Complete(dto.UserContext.UserId, dto.Now, null, null, null);
        SetDefaultValue(dto.UserContext);
        UpdateVersion();
    }

    /// <summary>HO-CAT-001 weight event (§8.6) -- idempotent: an item already Done with the same reference is left alone. Returns whether anything changed.</summary>
    public bool ApplyWeight(Guid weightRecordId, DateOnly recordedOn, DateOnly surgeryLocalDate, int validityDays, Guid by, DateTimeOffset now)
    {
        if (Status != SurgeryStatus.Scheduled && Status != SurgeryStatus.PreOp) return false;
        if (!PreOpChecklistRules.IsWeightInWindow(recordedOn, surgeryLocalDate, validityDays)) return false;

        var item = ItemOf(PreOpChecklistItemKind.WeightRecorded);
        if (item.Status == ChecklistItemStatus.Done && item.ReferenceId == weightRecordId) return false;

        item.Complete(by, now, null, weightRecordId, recordedOn);
        UpdateVersion();
        return true;
    }

    /// <summary>HO-EMR-001 consent events (§8.6) -- <paramref name="covered"/> is whether the consent records, re-read just now, cover every required kind.
    /// Completes the item when they do (idempotent) and reverts it when they no longer do.</summary>
    public bool ApplyConsentCoverage(bool covered, Guid? consentId, Guid by, DateTimeOffset now)
    {
        if (Status != SurgeryStatus.Scheduled && Status != SurgeryStatus.PreOp) return false;

        var item = ItemOf(PreOpChecklistItemKind.ConsentSigned);
        if (covered)
        {
            if (item.Status == ChecklistItemStatus.Done && item.ReferenceId == consentId) return false;
            item.Complete(by, now, null, consentId, null);
        }
        else
        {
            if (item.Status != ChecklistItemStatus.Done) return false;
            item.Revert();
        }

        UpdateVersion();
        return true;
    }

    /// <summary>
    /// US-SRG-03 / BR-5 (ADR-161). <paramref name="dto"/> carries consent as the handler re-read it from the consent records; this method never consults the checklist
    /// flag for consent. Consent recorded: start (an override sent anyway is ignored). Not recorded: no override -> <c>ConsentNotRecorded</c>; an override needs a vet and
    /// a reason of 10 to 1000 characters and is stored on the aggregate for good. Then every OTHER outstanding item blocks (<c>PreOpChecklistIncomplete</c>, named).
    /// </summary>
    public void Start(SurgeryStartDto dto)
    {
        SurgeryTransitionRules.EnsureAllowed(Status, SurgeryTransitionRules.Start);

        ConsentOverride? consentOverride = null;
        if (!dto.ConsentRecorded)
        {
            if (dto.OverrideReason is null)
                throw new DomainRuleViolationException(BusinessViolationCodes.ConsentNotRecorded, SurgeryMessages.ConsentNotRecorded, nameof(dto.ConsentRecorded));

            if (!dto.OverriderIsVet)
                throw new DomainRuleViolationException(BusinessViolationCodes.ConsentOverrideNotPermitted, "Only a veterinarian may override a missing consent.", nameof(dto.OverrideReason));

            var reason = dto.OverrideReason.Trim();
            if (reason.Length < SurgeryRules.OverrideReasonMin || reason.Length > SurgeryRules.OverrideReasonMax)
                throw new DomainRuleViolationException(BusinessViolationCodes.ConsentOverrideReasonRequired,
                    $"Give the reason for the emergency override ({SurgeryRules.OverrideReasonMin} to {SurgeryRules.OverrideReasonMax} characters).", nameof(dto.OverrideReason));

            consentOverride = new ConsentOverride(dto.UserContext.UserId, dto.Now, reason);
        }

        var outstanding = PreOpChecklistRules.Outstanding(_checklist, consentJudgedElsewhere: true);
        if (outstanding.Count > 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.PreOpChecklistIncomplete,
                $"The pre-op checklist is not complete: {PreOpChecklistRules.Describe(outstanding)}.", nameof(Checklist));

        // Nothing is recorded for a start that did not happen: the override is only stored once every refusal above has passed.
        var overridden = consentOverride is not null;
        ConsentOverride = consentOverride;

        Transition(SurgeryStatus.InTheatre, dto.Now, dto.UserContext.UserId);
        ProcedureStartedOn = dto.Now;

        SetDefaultValue(dto.UserContext);
        if (overridden)
            AddDomainEvent(new SurgeryConsentOverriddenEvent { SurgeryId = Id, CatId = CatId, BranchId = BranchId, OverriddenBy = dto.UserContext.UserId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        AddDomainEvent(new SurgeryStartedEvent { SurgeryId = Id, CatId = CatId, BranchId = BranchId, ConsentWasOverridden = overridden, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    // ── theatre to discharge (US-SRG-05, 06, 07, 10) ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Recovery starts at <c>Recovering</c>. A Completed spay or neuter raises <see cref="SterilisationCompletedEvent"/> with the branch-local date (US-SRG-06);
    /// an Abandoned procedure, or any other procedure, does not.</summary>
    public void EndProcedure(SurgeryEndProcedureDto dto)
    {
        SurgeryTransitionRules.EnsureAllowed(Status, SurgeryTransitionRules.EndProcedure);

        Transition(SurgeryStatus.Recovery, dto.Now, dto.UserContext.UserId);
        Outcome = dto.Outcome;
        ProcedureEndedOn = dto.Now;
        ProcedureEndedLocalDate = BranchCalendar.LocalDateOf(dto.Now, dto.TimeZoneId);
        RecoveryStatus = RecoveryStatusEnum.Recovering;
        AddRecovery(null, RecoveryStatusEnum.Recovering, dto.Now, dto.UserContext.UserId, null);

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new SurgeryProcedureEndedEvent { SurgeryId = Id, CatId = CatId, BranchId = BranchId, Outcome = dto.Outcome, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });

        if (dto.Outcome == ProcedureOutcome.Completed && Procedure.IsSterilisation)
            AddDomainEvent(new SterilisationCompletedEvent
            { SurgeryId = Id, CatId = CatId, BranchId = BranchId, CompletedOnLocalDate = ProcedureEndedLocalDate.Value, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });

        UpdateVersion();
    }

    /// <summary>Any of the three to any other; a change to the same status is a no-op (U-F1).</summary>
    public void SetRecoveryStatus(RecoveryStatusDto dto)
    {
        SurgeryTransitionRules.EnsureAllowed(Status, SurgeryTransitionRules.SetRecoveryStatus);
        if (dto.Note is { Length: > SurgeryRules.NoteMax })
            throw new DomainRuleViolationException(BusinessViolationCodes.SurgeryTextInvalid, $"A note is at most {SurgeryRules.NoteMax} characters.", nameof(dto.Note));
        if (RecoveryStatus == dto.Status) return;

        // §8.1: the owned history is bounded (SurgeryRules.MaxRecoveryHistory). One recovery stay will not reach it; a runaway client would.
        if (_recoveryHistory.Count >= SurgeryRules.MaxRecoveryHistory)
            throw new DomainRuleViolationException(BusinessViolationCodes.RecoveryHistoryLimitReached,
                $"The recovery status has been changed {SurgeryRules.MaxRecoveryHistory} times for this surgery; add a note to the clinical record instead.", nameof(dto.Status));

        AddRecovery(RecoveryStatus, dto.Status, dto.Now, dto.UserContext.UserId, dto.Note?.Trim());
        RecoveryStatus = dto.Status;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new RecoveryStatusChangedEvent { SurgeryId = Id, BranchId = BranchId, Status = dto.Status, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>
    /// Requires <c>RecoveryStatus == ReadyForDischarge</c> -- except that a DECEASED cat may be discharged (closed) from any recovery status, with no follow-ups and
    /// no instructions (U-F6). Otherwise the instructions (1 to 4000) are required, and each follow-up's due date must sit inside its protocol window (US-SRG-07).
    /// </summary>
    public void Discharge(SurgeryDischargeDto dto)
    {
        SurgeryTransitionRules.EnsureAllowed(Status, SurgeryTransitionRules.Discharge);

        if (!dto.CatIsDeceased)
        {
            if (RecoveryStatus != RecoveryStatusEnum.ReadyForDischarge)
                throw new DomainRuleViolationException(BusinessViolationCodes.NotReadyForDischarge, SurgeryMessages.NotReadyForDischarge, nameof(RecoveryStatus));

            var instructions = dto.PostOpInstructions?.Trim();
            if (string.IsNullOrEmpty(instructions) || instructions.Length > SurgeryRules.PostOpInstructionsMax)
                throw new DomainRuleViolationException(BusinessViolationCodes.PostOpInstructionsRequired,
                    $"Post-op instructions are required (up to {SurgeryRules.PostOpInstructionsMax} characters).", nameof(dto.PostOpInstructions));

            if (dto.FollowUps.Count > SurgeryRules.MaxFollowUps)
                throw new DomainRuleViolationException(BusinessViolationCodes.FollowUpOutsideWindow, $"A surgery has at most {SurgeryRules.MaxFollowUps} follow-ups.", nameof(dto.FollowUps));

            foreach (var followUp in dto.FollowUps)
                FollowUpPlanner.EnsureDateInWindow(new PlannedFollowUp(followUp.Kind, followUp.DueOn, followUp.WindowFrom, followUp.WindowTo), followUp.DueOn);

            PostOpInstructions = instructions;
            _followUps.Clear();
            foreach (var followUp in dto.FollowUps)
                _followUps.Add(new SurgeryFollowUp(followUp.Id, followUp.Kind, followUp.DueOn, followUp.WindowFrom, followUp.WindowTo));
        }

        CatWasDeceasedAtDischarge = dto.CatIsDeceased;
        Transition(SurgeryStatus.Discharged, dto.Now, dto.UserContext.UserId);
        DischargedOn = dto.Now;
        OwnerAtDischargeId = dto.OwnerAtDischargeId;
        RecoveryStatus = null;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new SurgeryDischargedEvent
        {
            SurgeryId = Id, CatId = CatId, BranchId = BranchId, CustomerId = CustomerId, ServiceId = ClinicServiceId, DischargedOn = dto.Now,
            PricingBasis = PriceSnapshot.Basis, CatWasDeceased = dto.CatIsDeceased, OwnerAtDischargeId = dto.OwnerAtDischargeId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    /// <summary>Completes or cancels a Pending follow-up. Closing it makes its pending reminder fail the <c>SurgeryFollowUp</c> guard.</summary>
    public void CloseFollowUp(FollowUpCloseDto dto)
    {
        SurgeryTransitionRules.EnsureAllowed(Status, SurgeryTransitionRules.CloseFollowUp);

        var followUp = _followUps.FirstOrDefault(f => f.Id == dto.FollowUpId)
            ?? throw new DomainRuleViolationException(BusinessViolationCodes.SurgeryFollowUpNotFound, "Follow-up not found.", nameof(dto.FollowUpId));
        if (followUp.Status != FollowUpStatus.Pending)
            throw new DomainRuleViolationException(BusinessViolationCodes.FollowUpNotPending, "This follow-up has already been closed.", nameof(dto.FollowUpId));

        var reason = dto.Reason?.Trim();
        if (dto.Status == FollowUpStatus.Cancelled && (string.IsNullOrEmpty(reason) || reason.Length > SurgeryRules.CancellationReasonMax))
            throw new DomainRuleViolationException(BusinessViolationCodes.CancellationReasonRequired,
                $"Give a reason (up to {SurgeryRules.CancellationReasonMax} characters).", nameof(dto.Reason));

        followUp.Close(dto.Status, dto.Now, dto.UserContext.UserId, dto.Status == FollowUpStatus.Cancelled ? reason : null);

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new SurgeryFollowUpClosedEvent { SurgeryId = Id, FollowUpId = followUp.Id, Status = dto.Status, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>HO-ACC-001 <c>CustomerMergedEvent</c> / HO-CAT-001 transfer (§11.6) -- idempotent.</summary>
    public bool ReassignOwner(Guid newCustomerId)
    {
        if (CustomerId == newCustomerId) return false;

        CustomerId = newCustomerId;
        UpdateVersion();
        return true;
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The checklist changes while the surgery is still before theatre.</summary>
    private void EnsureChecklistOpen()
    {
        if (Status != SurgeryStatus.Scheduled && Status != SurgeryStatus.PreOp)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidSurgeryTransition,
                $"The checklist of a surgery {StatusPhrase()} can no longer be changed.", nameof(Status));
    }

    private string StatusPhrase() =>
        Status == SurgeryStatus.InTheatre ? "in theatre" : Status == SurgeryStatus.Recovery ? "in recovery" : "that is " + Status.DisplayName.ToLowerInvariant();

    private PreOpChecklistItem ItemOf(PreOpChecklistItemKind kind) => _checklist.First(i => i.Kind == kind);

    private void Transition(SurgeryStatus to, DateTimeOffset now, Guid changedBy)
    {
        AddHistory(Status, to, now, changedBy);
        Status = to;
    }

    private void AddHistory(SurgeryStatus? from, SurgeryStatus to, DateTimeOffset now, Guid changedBy)
    {
        // Never trimmed (C-ALL-4); the state machine bounds the length. A monotonic sequence, so ordering never ties.
        var sequence = _statusHistory.Count == 0 ? 1 : _statusHistory.Max(h => h.Sequence) + 1;
        _statusHistory.Add(new SurgeryStatusChange(Guid.NewGuid(), from, to, now, changedBy, sequence));
    }

    private void AddRecovery(RecoveryStatus? from, RecoveryStatus to, DateTimeOffset now, Guid changedBy, string? note)
    {
        var sequence = _recoveryHistory.Count == 0 ? 1 : _recoveryHistory.Max(h => h.Sequence) + 1;
        _recoveryHistory.Add(new RecoveryStatusChange(Guid.NewGuid(), from, to, now, changedBy, note, sequence));
    }

    private static string Stamp(DateTimeOffset startsOn) => startsOn.ToString("O");
}
