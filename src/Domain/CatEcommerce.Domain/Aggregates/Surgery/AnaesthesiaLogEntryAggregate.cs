using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Surgery;

/// <summary>
/// HO-SRG-001 §8.8 (US-SRG-04, US-SRG-08, ADR-162) -- ONE entry of a surgery's anaesthesia log: a drug, a set of vitals, a milestone, or a correction of one of those. <b>Append-only:
/// an entry is created once and nothing changes it.</b> There is no Update, Delete or Void on this class, no setter outside it, no endpoint that edits one, and a PostgreSQL trigger
/// refuses any UPDATE or DELETE (the code path nobody intended). A correction is a NEW entry linked to the one it corrects; the corrected entry stays visible.
///
/// Its own root (not owned by <see cref="SurgeryAggregate"/>) so a nurse logging vitals every few minutes while the vet logs drugs never bumps the surgery row's <c>xmin</c>, and so
/// "append-only" is a property of a TABLE. Every entry is attributed (<see cref="LoggedBy"/>) and timestamped (<see cref="LoggedOn"/>) from the context and the clock, never from the caller.
///
/// The facts that live elsewhere arrive already decided by the handler: that the surgery is open for logging, the stock issue and the controlled-drug register entry (BR-3, BR-7).
/// This class checks what is checkable from its own values: the time window, the dose, the quantities, the plausibility of vitals, and the shape of a controlled drug entry.
/// </summary>
public sealed class AnaesthesiaLogEntryAggregate : AggregateRoot
{
    public Guid SurgeryId { get; private set; }
    public Guid CatId { get; private set; }
    public Guid BranchId { get; private set; }
    public AnaesthesiaEntryKind Kind { get; private set; } = default!;
    public DateTimeOffset OccurredOn { get; private set; }
    public Guid LoggedBy { get; private set; }
    public DateTimeOffset LoggedOn { get; private set; }

    // Drug
    public Guid? MedicineId { get; private set; }
    public string? MedicineName { get; private set; }
    public bool IsControlled { get; private set; }
    public decimal? DoseAmount { get; private set; }
    public DoseUnit? DoseUnit { get; private set; }
    public AdministrationRoute? Route { get; private set; }
    public Guid? AdministeredById { get; private set; }
    public Guid? SkuId { get; private set; }
    public Guid? BatchId { get; private set; }
    public string? BatchNumber { get; private set; }
    public Guid? LocationId { get; private set; }
    public decimal? QuantityDrawn { get; private set; }
    public decimal? QuantityWasted { get; private set; }
    public string? StockUnit { get; private set; }
    public Guid? WitnessId { get; private set; }
    public Guid? RegisterEntryId { get; private set; }
    public bool StockShortfall { get; private set; }

    // Vitals
    public int? HeartRateBpm { get; private set; }
    public int? RespiratoryRatePerMin { get; private set; }
    public int? SpO2Percent { get; private set; }
    public int? EtCo2MmHg { get; private set; }
    public decimal? TemperatureCelsius { get; private set; }
    public int? SystolicMmHg { get; private set; }
    public int? MeanArterialMmHg { get; private set; }
    public bool ImplausibleConfirmed { get; private set; }

    // Milestone
    public AnaesthesiaMilestone? Milestone { get; private set; }

    /// <summary>The note of a vitals entry or a milestone (up to 500 characters).</summary>
    public string? Note { get; private set; }

    // Correction
    public Guid? CorrectsEntryId { get; private set; }
    public AnaesthesiaEntryKind? CorrectedKind { get; private set; }
    public string? CorrectionReason { get; private set; }

    private AnaesthesiaLogEntryAggregate() { }

    public AnaesthesiaLogEntryAggregate(Guid id) : base(id) { }

    public void LogDrug(AnaesthesiaEntryContext context, AnaesthesiaDrugDto drug)
    {
        Begin(context, AnaesthesiaEntryKind.Drug);
        ApplyDrug(drug);
        Complete(context);
    }

    public void LogVitals(AnaesthesiaEntryContext context, AnaesthesiaVitalsDto vitals)
    {
        Begin(context, AnaesthesiaEntryKind.Vitals);
        ApplyVitals(vitals);
        Complete(context);
    }

    public void LogMilestone(AnaesthesiaEntryContext context, AnaesthesiaMilestoneDto milestone)
    {
        Begin(context, AnaesthesiaEntryKind.Milestone);
        ApplyMilestone(milestone);
        Complete(context);
    }

    /// <summary>A NEW entry that corrects another: <c>Kind = Correction</c>, linked by <c>CorrectsEntryId</c>, with a reason (10 to 500 characters) and the corrected values.</summary>
    public void Correct(AnaesthesiaEntryContext context, AnaesthesiaCorrectionDto correction)
    {
        Begin(context, AnaesthesiaEntryKind.Correction);

        var reason = correction.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length < SurgeryRules.CorrectionReasonMin || reason.Length > SurgeryRules.CorrectionReasonMax)
            throw Invalid($"Give the reason for the correction ({SurgeryRules.CorrectionReasonMin} to {SurgeryRules.CorrectionReasonMax} characters).", nameof(correction.Reason));
        if (correction.CorrectedKind == AnaesthesiaEntryKind.Correction)
            throw Invalid("A correction corrects the original entry; name the entry whose kind it is.", nameof(correction.CorrectedKind));
        if (correction.CorrectsEntryId == Id)
            throw Invalid("An entry cannot correct itself.", nameof(correction.CorrectsEntryId));

        var payloads = new[] { correction.Drug is not null, correction.Vitals is not null, correction.Milestone is not null }.Count(x => x);
        if (payloads != 1)
            throw Invalid("A correction carries the corrected values of exactly one kind.", nameof(correction.CorrectedKind));

        if (correction.Drug is not null)
        {
            if (correction.CorrectedKind != AnaesthesiaEntryKind.Drug) throw Invalid("The corrected values do not match the kind of the entry being corrected.", nameof(correction.CorrectedKind));
            ApplyDrug(correction.Drug);
        }
        else if (correction.Vitals is not null)
        {
            if (correction.CorrectedKind != AnaesthesiaEntryKind.Vitals) throw Invalid("The corrected values do not match the kind of the entry being corrected.", nameof(correction.CorrectedKind));
            ApplyVitals(correction.Vitals);
        }
        else
        {
            if (correction.CorrectedKind != AnaesthesiaEntryKind.Milestone) throw Invalid("The corrected values do not match the kind of the entry being corrected.", nameof(correction.CorrectedKind));
            ApplyMilestone(correction.Milestone!);
        }

        CorrectsEntryId = correction.CorrectsEntryId;
        CorrectedKind = correction.CorrectedKind;
        CorrectionReason = reason;
        Complete(context);
    }

    // ── shared steps ────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private void Begin(AnaesthesiaEntryContext context, AnaesthesiaEntryKind kind)
    {
        if (LoggedOn != default)
            throw Invalid("This entry has already been recorded; a log entry is never changed.", nameof(LoggedOn));

        if (context.OccurredOn > context.Now.Add(SurgeryRules.FutureTolerance))
            throw Invalid("An entry cannot be dated in the future.", nameof(context.OccurredOn));
        if (context.AdmittedOn is { } admittedOn && context.OccurredOn < admittedOn.Subtract(SurgeryRules.BeforeAdmissionTolerance))
            throw Invalid("An entry cannot be dated more than an hour before the cat was admitted.", nameof(context.OccurredOn));

        SurgeryId = context.SurgeryId;
        CatId = context.CatId;
        BranchId = context.BranchId;
        Kind = kind;
        OccurredOn = context.OccurredOn;
    }

    private void Complete(AnaesthesiaEntryContext context)
    {
        LoggedBy = context.UserContext.UserId;
        LoggedOn = context.Now;

        SetDefaultValue(context.UserContext);
        AddDomainEvent(new AnaesthesiaEntryLoggedEvent
        {
            EntryId = Id, SurgeryId = SurgeryId, CatId = CatId, BranchId = BranchId, Kind = Kind, IsControlled = IsControlled,
            CorrelationId = context.CorrelationId, UserContext = context.UserContext
        });
        UpdateVersion();
    }

    private void ApplyDrug(AnaesthesiaDrugDto drug)
    {
        if (drug.DoseAmount <= 0m || decimal.Round(drug.DoseAmount, 3) != drug.DoseAmount || drug.DoseAmount > 9999999m)
            throw Invalid("The dose must be greater than zero, with at most three decimal places.", nameof(drug.DoseAmount));
        if (string.IsNullOrWhiteSpace(drug.MedicineName) || drug.MedicineName.Length > 200)
            throw Invalid("The medicine name is required.", nameof(drug.MedicineName));

        if (drug.QuantityDrawn is < 0m) throw Invalid("The quantity drawn cannot be negative.", nameof(drug.QuantityDrawn));
        if (drug.QuantityWasted is < 0m) throw Invalid("The quantity wasted cannot be negative.", nameof(drug.QuantityWasted));
        if (drug.QuantityWasted is { } wasted && (drug.QuantityDrawn is not { } drawn || wasted > drawn))
            throw Invalid("The quantity wasted cannot be more than the quantity drawn.", nameof(drug.QuantityWasted));

        if (drug.IsControlled)
        {
            // BR-7: the administering vet, a witness who is someone else, the wastage (zero is allowed) and the register entry the handler enlisted.
            if (drug.WitnessId is null || drug.WitnessId == Guid.Empty)
                throw new DomainRuleViolationException(BusinessViolationCodes.WitnessRequired, "A controlled drug needs a second member of staff as witness.", nameof(drug.WitnessId));
            if (drug.WitnessId == drug.AdministeredById)
                throw new DomainRuleViolationException(BusinessViolationCodes.WitnessSameAsAdministrator, "The witness must be a different person from the administering vet.", nameof(drug.WitnessId));
            if (drug.QuantityWasted is null)
                throw new DomainRuleViolationException(BusinessViolationCodes.WastageRequired, "Record the quantity wasted (0 when none) for a controlled drug.", nameof(drug.QuantityWasted));
            if (drug.RegisterEntryId is null)
                throw Invalid("A controlled drug is recorded with its register entry.", nameof(drug.RegisterEntryId));
        }

        MedicineId = drug.MedicineId;
        MedicineName = drug.MedicineName.Trim();
        IsControlled = drug.IsControlled;
        DoseAmount = drug.DoseAmount;
        DoseUnit = drug.DoseUnit;
        Route = drug.Route;
        AdministeredById = drug.AdministeredById;
        SkuId = drug.SkuId;
        BatchId = drug.BatchId;
        BatchNumber = drug.BatchNumber;
        LocationId = drug.LocationId;
        QuantityDrawn = drug.QuantityDrawn;
        QuantityWasted = drug.QuantityWasted;
        StockUnit = drug.StockUnit;
        WitnessId = drug.WitnessId;
        RegisterEntryId = drug.RegisterEntryId;
        StockShortfall = drug.StockShortfall;
    }

    private void ApplyVitals(AnaesthesiaVitalsDto v)
    {
        if (v.HeartRateBpm is null && v.RespiratoryRatePerMin is null && v.SpO2Percent is null && v.EtCo2MmHg is null && v.TemperatureCelsius is null
            && v.SystolicMmHg is null && v.MeanArterialMmHg is null)
            throw new DomainRuleViolationException(BusinessViolationCodes.VitalsRequired, "Record at least one vital sign.", nameof(v.HeartRateBpm));

        if (v.Note is { Length: > SurgeryRules.NoteMax }) throw Invalid($"A note is at most {SurgeryRules.NoteMax} characters.", nameof(v.Note));

        var implausible = new List<string>();
        void Check(string name, decimal? value, (decimal Min, decimal Max) range)
        {
            if (value is { } x && !AnaesthesiaVitalsRanges.IsPlausible(range, x)) implausible.Add($"{name} {x}");
        }

        Check("heart rate", v.HeartRateBpm, AnaesthesiaVitalsRanges.HeartRateBpm);
        Check("respiratory rate", v.RespiratoryRatePerMin, AnaesthesiaVitalsRanges.RespiratoryRatePerMin);
        Check("SpO2", v.SpO2Percent, AnaesthesiaVitalsRanges.SpO2Percent);
        Check("EtCO2", v.EtCo2MmHg, AnaesthesiaVitalsRanges.EtCo2MmHg);
        Check("temperature", v.TemperatureCelsius, AnaesthesiaVitalsRanges.TemperatureCelsius);
        Check("systolic pressure", v.SystolicMmHg, AnaesthesiaVitalsRanges.SystolicMmHg);
        Check("mean arterial pressure", v.MeanArterialMmHg, AnaesthesiaVitalsRanges.MeanArterialMmHg);

        if (implausible.Count > 0 && !v.ConfirmImplausible)
            throw new DomainRuleViolationException(BusinessViolationCodes.ImplausibleVitalValue,
                $"Check these values, or confirm them if they are right: {string.Join(", ", implausible)}.", nameof(v.ConfirmImplausible));

        if (v.TemperatureCelsius is { } t && decimal.Round(t, 1) != t) throw Invalid("A temperature has at most one decimal place.", nameof(v.TemperatureCelsius));

        HeartRateBpm = v.HeartRateBpm;
        RespiratoryRatePerMin = v.RespiratoryRatePerMin;
        SpO2Percent = v.SpO2Percent;
        EtCo2MmHg = v.EtCo2MmHg;
        TemperatureCelsius = v.TemperatureCelsius;
        SystolicMmHg = v.SystolicMmHg;
        MeanArterialMmHg = v.MeanArterialMmHg;
        ImplausibleConfirmed = implausible.Count > 0;
        Note = v.Note?.Trim();
    }

    private void ApplyMilestone(AnaesthesiaMilestoneDto m)
    {
        if (m.Note is { Length: > SurgeryRules.NoteMax }) throw Invalid($"A note is at most {SurgeryRules.NoteMax} characters.", nameof(m.Note));

        Milestone = m.Milestone;
        Note = m.Note?.Trim();
    }

    private static DomainRuleViolationException Invalid(string message, string property) => new(BusinessViolationCodes.InvalidAnaesthesiaEntry, message, property);
}
