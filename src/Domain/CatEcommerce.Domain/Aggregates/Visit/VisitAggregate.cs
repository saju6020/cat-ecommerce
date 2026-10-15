using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Visit;

/// <summary>
/// HO-EMR-001 §8.2 -- the consultation record of one appointment (ADR-117: the <c>Visit</c> roots live
/// in the Clinic context, schema <c>clinic</c>). Everything a vet writes <i>during</i> the visit and
/// that freezes <i>with</i> it is owned here: vitals, problems, photographs, the SOAP note and the
/// follow-up. Things that legitimately continue <i>after</i> finalisation are separate roots (addenda,
/// diagnostic orders, consent), and so is the per-author draft working copy (ADR-122).
///
/// <b>Immutability, layer one (ADR-118).</b> <see cref="EnsureOpen"/> is the first statement of every
/// mutation method except <see cref="Open"/>; there is no method that changes a finalised visit --
/// not even for administrators (BR-11, D-5). Layer two is "no API path"; layer three is the PostgreSQL
/// triggers of the <c>AddMedicalRecordsModule</c> migration.
///
/// The visit holds <b>no customer id</b>: owner-facing reads resolve the cat's <i>current</i> owner
/// (FR-CAT-8), and a frozen row cannot follow an ownership transfer or a customer merge.
/// <b>No stock quantity of the shelf is stored</b> on any clinical table (D-3) -- a treatment line records what it took
/// and the batch it took it from; the movement itself is Inventory's ledger entry, enlisted by the handler in the same
/// commit (HO-EMR-001 Phase F, ADR-126).
/// </summary>
public sealed class VisitAggregate : AggregateRoot
{
    /// <summary>A client clock may run slightly ahead of the server's; a measurement or photograph date
    /// within this tolerance of "now" is not "in the future".</summary>
    private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromMinutes(5);

    private readonly List<VisitProblem> _problems = [];
    private readonly List<VitalsEntry> _vitals = [];
    private readonly List<ClinicalPhoto> _photos = [];
    private readonly List<TreatmentLine> _treatments = [];

    public Guid BranchId { get; private set; }
    public VisitOrigin Origin { get; private set; } = VisitOrigin.Appointment;
    public Guid AppointmentId { get; private set; }
    public Guid CatId { get; private set; }

    // Named ClinicServiceId, not ServiceId -- the platform's Entity base already declares ServiceId.
    public Guid ClinicServiceId { get; private set; }
    public string ServiceName { get; private set; } = default!;
    public ServiceCategory ServiceCategory { get; private set; } = default!;

    public VisitStatus Status { get; private set; } = VisitStatus.Open;
    public DateTimeOffset OpenedOn { get; private set; }
    public Guid OpenedBy { get; private set; }

    public SoapNote Note { get; private set; } = SoapNote.Empty;
    public FollowUp? FollowUp { get; private set; }

    public DateTimeOffset? FinalisedOn { get; private set; }

    /// <summary>The attending vet of record.</summary>
    public Guid? FinalisedBy { get; private set; }

    /// <summary>The cat's owner at the moment the visit was finalised -- the customer Billing invoices (HO-BIL-001 Phase J, owner decision 2026-10-14).
    /// Set once by <see cref="Finalise"/> and never changed; null on a visit finalised before this column existed (Billing then refuses to guess).</summary>
    public Guid? CustomerId { get; private set; }

    public IReadOnlyList<VisitProblem> Problems => _problems;
    public IReadOnlyList<VitalsEntry> Vitals => _vitals;
    public IReadOnlyList<ClinicalPhoto> Photos => _photos;
    public IReadOnlyList<TreatmentLine> Treatments => _treatments;

    private VisitAggregate() { }

    public VisitAggregate(Guid id) : base(id) { }

    /// <summary>Problems still on the record (withdrawn ones are kept but ignored).</summary>
    public IReadOnlyList<VisitProblem> ActiveProblems => _problems.Where(p => p.IsActive).ToList();

    // ── Open ───────────────────────────────────────────────────────────────────────────────────

    public void Open(VisitOpeningDto dto)
    {
        if (dto.AppointmentStatus != AppointmentStatus.CheckedIn && dto.AppointmentStatus != AppointmentStatus.InConsultation)
            throw Violation(BusinessViolationCodes.AppointmentNotReadyForVisit, ClinicalMessages.AppointmentNotReadyForVisit, nameof(dto.AppointmentStatus));

        if (dto.CatLifeStatus != CatLifeStatus.Alive)
            throw Violation(BusinessViolationCodes.CatIsDeceased, "A visit cannot be opened for a deceased cat.", nameof(dto.CatId));

        BranchId = dto.BranchId;
        Origin = VisitOrigin.Appointment;
        AppointmentId = dto.AppointmentId;
        CatId = dto.CatId;
        ClinicServiceId = dto.ClinicServiceId;
        ServiceName = dto.ServiceName.Length > ClinicalNoteRules.MaxServiceNameLength
            ? dto.ServiceName[..ClinicalNoteRules.MaxServiceNameLength]
            : dto.ServiceName;
        ServiceCategory = dto.ServiceCategory;
        Status = VisitStatus.Open;
        OpenedOn = dto.Now;
        OpenedBy = dto.UserContext.UserId;
        Note = SoapNote.Empty;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new VisitOpenedEvent
        {
            VisitId = Id, AppointmentId = AppointmentId, CatId = CatId, BranchId = BranchId,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    // ── Vitals (US-CLN-11, ADR-120) ────────────────────────────────────────────────────────────

    /// <summary>Two-tier range check: outside the <i>absolute</i> range is refused outright; outside the
    /// <i>plausible</i> range is refused until the request lists that sign in
    /// <see cref="VitalsDto.ConfirmedOutOfRange"/>, and the confirmation is stored on the entry. The
    /// weight, when present, is also written to the cat's weight history by the handler in the same
    /// commit with the same id (ADR-121).</summary>
    public void RecordVitals(VitalsDto dto)
    {
        EnsureOpen();

        if (_vitals.Count >= ClinicalNoteRules.MaxVitalsEntries)
            throw Violation(BusinessViolationCodes.ClinicalLimitExceeded, $"A visit holds at most {ClinicalNoteRules.MaxVitalsEntries} sets of vital signs.", nameof(Vitals));

        var values = new Dictionary<VitalSign, decimal>();
        if (dto.WeightKg is { } weight) values[VitalSign.Weight] = weight;
        if (dto.TemperatureC is { } temperature) values[VitalSign.Temperature] = temperature;
        if (dto.HeartRateBpm is { } heartRate) values[VitalSign.HeartRate] = heartRate;
        if (dto.RespiratoryRatePerMin is { } respiratoryRate) values[VitalSign.RespiratoryRate] = respiratoryRate;
        if (dto.BodyConditionScore is { } bodyCondition) values[VitalSign.BodyConditionScore] = bodyCondition;
        if (dto.PainScore is { } pain) values[VitalSign.PainScore] = pain;

        if (values.Count == 0)
            throw Violation(BusinessViolationCodes.VitalsRequired, ClinicalMessages.VitalsRequired, nameof(dto.WeightKg));

        if (dto.Note is { Length: > ClinicalNoteRules.MaxVitalsNoteLength })
            throw Violation(BusinessViolationCodes.ClinicalTextTooLong, "The note is too long.", nameof(dto.Note));

        if (dto.MeasuredOn > dto.Now.Add(ClockSkewTolerance))
            throw Violation(BusinessViolationCodes.ClinicalDateInFuture, "The measurement time cannot be in the future.", nameof(dto.MeasuredOn));

        var check = VitalRangeCheck.Evaluate(values);

        if (check.AbsoluteViolations.Count > 0)
        {
            var sign = check.AbsoluteViolations[0];
            var range = VitalSignRules.For(sign);
            throw Violation(
                BusinessViolationCodes.VitalSignOutOfRange,
                $"{sign.DisplayName} must be between {VitalSignRules.Format(sign, range.AbsoluteMin)} and {VitalSignRules.Format(sign, range.AbsoluteMax)} {range.Unit}. Check the value and try again.",
                sign.DisplayName);
        }

        var unconfirmed = check.PlausibleWarnings.Where(s => !dto.ConfirmedOutOfRange.Contains(s)).ToList();
        if (unconfirmed.Count > 0)
        {
            var listed = string.Join(", ", unconfirmed.Select(s => s.DisplayName));
            throw Violation(
                BusinessViolationCodes.VitalsOutsidePlausibleRange,
                $"{ClinicalMessages.VitalsOutsidePlausibleRange} ({listed})",
                unconfirmed[0].DisplayName);
        }

        var confirmed = check.PlausibleWarnings.Where(s => dto.ConfirmedOutOfRange.Contains(s)).ToList();

        _vitals.Add(new VitalsEntry(
            dto.VitalsEntryId, dto.MeasuredOn, dto.WeightKg, dto.TemperatureC, dto.HeartRateBpm, dto.RespiratoryRatePerMin,
            dto.BodyConditionScore, dto.PainScore, confirmed, dto.Note, dto.RecordedBy, dto.Now));

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new VisitVitalsRecordedEvent
        {
            VisitId = Id, VitalsEntryId = dto.VitalsEntryId, HasWeight = dto.WeightKg is not null,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    // ── Note and coded problems (US-CLN-12, ADR-122, ADR-123) ──────────────────────────────────

    /// <summary>
    /// Replaces the note wholesale and reconciles the coded problem list. A problem the caller omits is
    /// <b>withdrawn</b>, never deleted (C-ALL-4); a problem whose id and code are unchanged is updated
    /// in place and may keep a code that was deactivated since; any other problem needs an <i>active</i>
    /// code. No code may appear twice.
    /// </summary>
    public void SaveNote(SoapNoteDto dto)
    {
        EnsureOpen();

        ValidateNoteLengths(dto);

        if (dto.Problems.Count > ClinicalNoteRules.MaxProblems)
            throw Violation(BusinessViolationCodes.ClinicalLimitExceeded, $"A visit holds at most {ClinicalNoteRules.MaxProblems} problems.", nameof(dto.Problems));

        if (dto.Problems.GroupBy(p => p.DiagnosisCodeId).Any(g => g.Count() > 1))
            throw Violation(BusinessViolationCodes.DuplicateProblem, "The same diagnosis code cannot be listed twice.", nameof(dto.Problems));

        foreach (var problem in dto.Problems)
        {
            if (problem.Note is { Length: > ClinicalNoteRules.MaxProblemNoteLength })
                throw Violation(BusinessViolationCodes.ClinicalTextTooLong, "A problem note is too long.", nameof(problem.Note));

            var existing = _problems.FirstOrDefault(p => p.Id == problem.ProblemId);
            var unchangedCode = existing is not null && existing.DiagnosisCodeId == problem.DiagnosisCodeId;
            if (!unchangedCode && !problem.CodeIsActive)
                throw Violation(BusinessViolationCodes.DiagnosisCodeInactive, $"The diagnosis code {problem.Code} is no longer in use. Choose another.", nameof(problem.DiagnosisCodeId));
        }

        var keptIds = dto.Problems.Select(p => p.ProblemId).ToHashSet();
        foreach (var existing in _problems.Where(p => p.IsActive && !keptIds.Contains(p.Id)))
            existing.Withdraw(dto.Now);

        foreach (var problem in dto.Problems)
        {
            var existing = _problems.FirstOrDefault(p => p.Id == problem.ProblemId);
            if (existing is null)
            {
                _problems.Add(new VisitProblem(problem.ProblemId, problem.DiagnosisCodeId, problem.Code, problem.Name, problem.Certainty, problem.Note, dto.Now));
            }
            else if (existing.DiagnosisCodeId == problem.DiagnosisCodeId)
            {
                // Same code: keep the original snapshot of its code and name (U-C2).
                existing.Update(existing.DiagnosisCodeId, existing.Code, existing.Name, problem.Certainty, problem.Note);
            }
            else
            {
                existing.Update(problem.DiagnosisCodeId, problem.Code, problem.Name, problem.Certainty, problem.Note);
            }
        }

        Note = new SoapNote
        {
            Subjective = dto.Subjective,
            Objective = dto.Objective,
            Assessment = dto.Assessment,
            Plan = dto.Plan,
            DischargeInstructions = dto.DischargeInstructions,
            NoteSavedOn = dto.Now,
            NoteSavedBy = dto.SavedBy,
            AppliedTemplateId = dto.AppliedTemplateId ?? Note.AppliedTemplateId,
            AppliedTemplateVersion = dto.AppliedTemplateId is null ? Note.AppliedTemplateVersion : dto.AppliedTemplateVersion
        };

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new ConsultationNoteSavedEvent
        {
            VisitId = Id, ActiveProblemCount = ActiveProblems.Count,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    // ── Photographs (US-CLN-19) ────────────────────────────────────────────────────────────────

    /// <summary>The file's purpose (<c>ClinicalPhoto</c>) and "attached once" are the handler's checks
    /// (the second backed by a unique index).</summary>
    public void AttachPhoto(ClinicalPhotoDto dto)
    {
        EnsureOpen();

        if (_photos.Count >= ClinicalNoteRules.MaxPhotos)
            throw Violation(BusinessViolationCodes.ClinicalLimitExceeded, $"A visit holds at most {ClinicalNoteRules.MaxPhotos} photographs.", nameof(Photos));

        if (dto.TakenOn > dto.Today)
            throw Violation(BusinessViolationCodes.ClinicalDateInFuture, "The photograph date cannot be in the future.", nameof(dto.TakenOn));

        if (dto.Note is { Length: > ClinicalNoteRules.MaxPhotoNoteLength })
            throw Violation(BusinessViolationCodes.ClinicalTextTooLong, "The note is too long.", nameof(dto.Note));

        _photos.Add(new ClinicalPhoto(dto.PhotoId, dto.FileId, dto.TakenOn, dto.Note, dto.Now, dto.AddedBy));

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new ClinicalPhotoAttachedEvent
        {
            VisitId = Id, PhotoId = dto.PhotoId, FileId = dto.FileId,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    // ── Treatments (US-CLN-15, ADR-126, ADR-127) ───────────────────────────────────────────────

    /// <summary>
    /// Records one treatment line. The handler has already resolved everything outside the visit -- the SKU, the batch, how much the dispensary could
    /// supply, the price at now -- and passes it in; this method checks that it is coherent and that the visit is open. It does <b>not</b> move stock:
    /// the handler enlists Inventory's ledger entry, and one save commits both (F23, ADR-126).
    /// </summary>
    public TreatmentLine RecordTreatment(TreatmentDto dto)
    {
        EnsureOpen();

        if (_treatments.Count >= ClinicalNoteRules.MaxTreatmentLines)
            throw Violation(BusinessViolationCodes.ClinicalLimitExceeded, $"A visit holds at most {ClinicalNoteRules.MaxTreatmentLines} treatment lines.", nameof(Treatments));

        if (string.IsNullOrWhiteSpace(dto.Description))
            throw Violation(BusinessViolationCodes.TreatmentDescriptionRequired, ClinicalMessages.TreatmentDescriptionRequired, nameof(dto.Description));
        if (dto.Description.Length > ClinicalNoteRules.MaxTreatmentDescriptionLength)
            throw Violation(BusinessViolationCodes.ClinicalTextTooLong, $"The description cannot exceed {ClinicalNoteRules.MaxTreatmentDescriptionLength} characters.", nameof(dto.Description));
        if (dto.Dose is { Length: > ClinicalNoteRules.MaxDoseLength })
            throw Violation(BusinessViolationCodes.ClinicalTextTooLong, $"The dose cannot exceed {ClinicalNoteRules.MaxDoseLength} characters.", nameof(dto.Dose));
        if (dto.Route is { Length: > ClinicalNoteRules.MaxRouteLength })
            throw Violation(BusinessViolationCodes.ClinicalTextTooLong, $"The route cannot exceed {ClinicalNoteRules.MaxRouteLength} characters.", nameof(dto.Route));
        if (dto.BatchOverrideReason is { Length: > ClinicalNoteRules.MaxBatchOverrideReasonLength })
            throw Violation(BusinessViolationCodes.ClinicalTextTooLong, $"The reason cannot exceed {ClinicalNoteRules.MaxBatchOverrideReasonLength} characters.", nameof(dto.BatchOverrideReason));

        if (dto.Quantity <= 0m || dto.Quantity > ClinicalNoteRules.MaxTreatmentQuantity || decimal.Round(dto.Quantity, 3) != dto.Quantity)
            throw Violation(BusinessViolationCodes.InvalidQuantity, "The quantity must be greater than zero, with at most three decimal places.", nameof(dto.Quantity));

        if (dto.DeductedQuantity < 0m || dto.ShortfallQuantity < 0m || (dto.Kind.MovesStock && dto.DeductedQuantity + dto.ShortfallQuantity != dto.Quantity))
            throw Violation(BusinessViolationCodes.InvalidQuantity, "The quantity taken from stock and the shortfall must add up to the quantity given.", nameof(dto.DeductedQuantity));

        if (dto.Kind.MovesStock)
        {
            if (dto.SkuId is null)
                throw Violation(BusinessViolationCodes.SkuNotFound, "Choose the item from the dispensary.", nameof(dto.SkuId));
            if (dto.DeductedQuantity > 0m && (dto.BatchId is null || dto.LocationId is null))
                throw Violation(BusinessViolationCodes.BatchNotIssuable, ClinicalMessages.BatchNotIssuable, nameof(dto.BatchId));
        }
        else if (dto.SkuId is not null || dto.BatchId is not null || dto.DeductedQuantity != 0m || dto.ShortfallQuantity != 0m)
        {
            throw Violation(BusinessViolationCodes.InvalidQuantity, "A procedure does not take stock. Record the items used as consumables.", nameof(dto.Kind));
        }

        if (dto.IsBillable && (dto.UnitPrice is null || dto.TaxClassId is null))
        {
            throw dto.Kind.MovesStock
                ? Violation(BusinessViolationCodes.SkuNotPriced, ClinicalMessages.SkuNotPriced, nameof(dto.IsBillable))
                : Violation(BusinessViolationCodes.ServiceNotPriced, "Choose a priced service to bill this procedure, or record it as not billable.", nameof(dto.IsBillable));
        }

        if (_treatments.Any(t => t.Id == dto.LineId))
            throw Violation(BusinessViolationCodes.TreatmentLineAlreadyRecorded, "That treatment line was already recorded.", nameof(dto.LineId));

        var line = new TreatmentLine(
            dto.LineId, dto.Kind, dto.Description.Trim(), dto.ClinicServiceId, dto.SkuId, dto.SkuName, dto.StockUnit,
            dto.BatchId, dto.BatchId is null ? null : dto.LotNumber, dto.BatchId is null ? null : dto.BatchExpiresOn, dto.LocationId,
            dto.Quantity, dto.DeductedQuantity, dto.ShortfallQuantity, dto.Dose?.Trim(), dto.Route?.Trim(), dto.IsBillable,
            dto.IsBillable ? dto.UnitPrice : null, dto.IsBillable ? dto.TaxClassId : null, dto.BatchOverrideReason?.Trim(), dto.RecordedBy, dto.Now, dto.Position);
        _treatments.Add(line);

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new TreatmentRecordedEvent
        {
            VisitId = Id, LineId = line.Id, Kind = line.Kind, IsBillable = line.IsBillable,
            DeductedQuantity = line.DeductedQuantity, ShortfallQuantity = line.ShortfallQuantity,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });

        if (line.ShortfallQuantity > 0m && line.SkuId is { } skuId)
        {
            AddDomainEvent(new ClinicalStockShortfallRecordedEvent
            {
                VisitId = Id, LineId = line.Id, BranchId = BranchId, SkuId = skuId,
                ShortfallQuantity = line.ShortfallQuantity, DeductedQuantity = line.DeductedQuantity,
                CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
            });
        }

        UpdateVersion();
        return line;
    }

    /// <summary>The line becomes <c>Reversed</c>; it is never removed. When stock had been deducted the handler enlists the compensating movement
    /// in the same commit (<see cref="TreatmentLine.DeductedQuantity"/> &gt; 0).</summary>
    public TreatmentLine ReverseTreatment(TreatmentReversalDto dto)
    {
        EnsureOpen();

        var line = _treatments.FirstOrDefault(t => t.Id == dto.LineId)
            ?? throw Violation(BusinessViolationCodes.TreatmentLineNotFound, ClinicalMessages.TreatmentLineNotFound, nameof(dto.LineId));

        if (line.IsReversed)
            throw Violation(BusinessViolationCodes.TreatmentAlreadyReversed, ClinicalMessages.TreatmentAlreadyReversed, nameof(dto.LineId));

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw Violation(BusinessViolationCodes.ReversalReasonRequired, ClinicalMessages.ReversalReasonRequired, nameof(dto.Reason));
        if (dto.Reason.Trim().Length > ClinicalNoteRules.MaxTreatmentReversalReasonLength)
            throw Violation(BusinessViolationCodes.ClinicalTextTooLong, $"The reason cannot exceed {ClinicalNoteRules.MaxTreatmentReversalReasonLength} characters.", nameof(dto.Reason));

        line.Reverse(dto.Reason.Trim(), dto.ReversedBy, dto.Now);

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new TreatmentReversedEvent
        {
            VisitId = Id, LineId = line.Id, StockReturned = line.DeductedQuantity > 0m,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
        return line;
    }

    // ── Follow-up (US-CLN-18) ──────────────────────────────────────────────────────────────────

    public void SetFollowUp(FollowUpDto dto)
    {
        EnsureOpen();

        if (dto.DueOn <= dto.Today)
            throw Violation(BusinessViolationCodes.FollowUpDateInPast, "The follow-up date must be after today.", nameof(dto.DueOn));

        if (dto.Kind == FollowUpKind.Appointment)
        {
            if (dto.AppointmentId is null || !dto.AppointmentValid)
                throw Violation(BusinessViolationCodes.FollowUpAppointmentInvalid, "Link a booked, future appointment for this cat.", nameof(dto.AppointmentId));
        }
        else if (dto.AppointmentId is not null)
        {
            throw Violation(BusinessViolationCodes.FollowUpAppointmentInvalid, "A reminder follow-up has no appointment.", nameof(dto.AppointmentId));
        }

        // A new stamp every time: a reminder already queued for the previous follow-up carries the old
        // stamp and fails its send-time guard (ADR-131).
        FollowUp = new FollowUp(dto.Kind, dto.DueOn, dto.AppointmentId, Guid.NewGuid().ToString("N"), dto.Now, dto.SetBy);

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new FollowUpSetEvent
        {
            VisitId = Id, Kind = dto.Kind, DueOn = dto.DueOn,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    public void ClearFollowUp(FollowUpClearDto dto)
    {
        EnsureOpen();

        if (FollowUp is null) return;

        FollowUp = null;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new FollowUpClearedEvent { VisitId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    // ── Finalise (US-CLN-17, US-CLN-22, ADR-119, ADR-123) ──────────────────────────────────────

    /// <summary>Order of checks: a second finalise is <c>VisitFinalised</c> before anything else; then the
    /// caller must hold <c>Vet</c> (re-checked here although the endpoint already requires it -- an
    /// "Administrator can do everything" assumption would violate Appendix D, BR-6); then at least one
    /// active problem; then a non-blank assessment and plan.</summary>
    public void Finalise(VisitFinalisationDto dto)
    {
        EnsureOpen();

        if (!dto.FinaliserRoles.Contains(StaffRoles.Vet, StringComparer.Ordinal))
            throw Violation(BusinessViolationCodes.OnlyVeterinarianMayFinalise, ClinicalMessages.OnlyVeterinarianMayFinalise, nameof(dto.FinaliserRoles));

        if (ActiveProblems.Count == 0)
            throw Violation(BusinessViolationCodes.ProblemRequired, ClinicalMessages.ProblemRequired, nameof(Problems));

        if (string.IsNullOrWhiteSpace(Note.Assessment) || string.IsNullOrWhiteSpace(Note.Plan))
            throw Violation(BusinessViolationCodes.AssessmentAndPlanRequired, ClinicalMessages.AssessmentAndPlanRequired, nameof(Note));

        if (dto.CustomerId == Guid.Empty)
            throw Violation(BusinessViolationCodes.CatNotFound, "The cat's owner could not be determined, so the visit cannot be finalised.", nameof(dto.CustomerId));

        Status = VisitStatus.Finalised;
        FinalisedOn = dto.Now;
        FinalisedBy = dto.FinalisedBy;
        CustomerId = dto.CustomerId;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new VisitFinalisedEvent
        {
            VisitId = Id, AppointmentId = AppointmentId, CatId = CatId, BranchId = BranchId, CustomerId = dto.CustomerId,
            FinalisedBy = dto.FinalisedBy, FinalisedOn = dto.Now,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    // ── Guards ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>The one guard (ADR-118, layer one). Every mutation method except <see cref="Open"/> calls it
    /// first; U-C7 proves it by reflection.</summary>
    private void EnsureOpen()
    {
        if (Status != VisitStatus.Open)
            throw new DomainRuleViolationException(BusinessViolationCodes.VisitFinalised, ClinicalMessages.VisitFinalised, nameof(Status));
    }

    private static void ValidateNoteLengths(SoapNoteDto dto)
    {
        foreach (var (name, text, max) in new (string, string, int)[]
                 {
                     (nameof(dto.Subjective), dto.Subjective, ClinicalNoteRules.MaxSoapFieldLength),
                     (nameof(dto.Objective), dto.Objective, ClinicalNoteRules.MaxSoapFieldLength),
                     (nameof(dto.Assessment), dto.Assessment, ClinicalNoteRules.MaxSoapFieldLength),
                     (nameof(dto.Plan), dto.Plan, ClinicalNoteRules.MaxSoapFieldLength),
                     (nameof(dto.DischargeInstructions), dto.DischargeInstructions, ClinicalNoteRules.MaxDischargeInstructionsLength),
                 })
        {
            if (text.Length > max)
                throw Violation(BusinessViolationCodes.ClinicalTextTooLong, $"{name} cannot exceed {max} characters.", name);
        }
    }

    private static DomainRuleViolationException Violation(string code, string message, string property) => new(code, message, property);
}
