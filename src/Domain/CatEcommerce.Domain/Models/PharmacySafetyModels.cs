using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

// HO-PHM-001 §8.5 / §8.6 -- the inputs and outputs of the pure safety assessor (ADR-139 … ADR-141).

/// <summary>The cat's newest weight (HO-CAT-001 <c>cat_weight_record</c>, ADR-023) and the day it was recorded.</summary>
public sealed record LatestWeight(decimal Kilograms, DateOnly RecordedOn);

/// <summary>One recorded allergy of the cat (HO-CAT-001): free text, so matching is a normalised term match (ADR-140).</summary>
public sealed record AllergyFact(string Allergen, string Reaction, AllergySeverity Severity);

public enum ActiveMedicineSource
{
    Prescription,
    ProfileMedication
}

/// <summary>A medicine the cat is already taking: an active prescription, or a profile medication that resolved to a formulary entry by name.</summary>
public sealed record ActiveMedicineFact(
    Guid? MedicineId,
    string Name,
    IReadOnlyList<string> Ingredients,
    IReadOnlyList<string> Classes,
    IReadOnlyList<InteractionSnapshot> Interactions,
    ActiveMedicineSource Source,
    string? PrescriptionNumber);

public sealed record SafetyInput(
    FormularyMedicineSnapshot Medicine,
    Dose Dose,
    AdministrationRoute Route,
    DoseFrequency Frequency,
    string CatName,
    LatestWeight? LatestWeight,
    DateOnly Today,
    int WeightMaxAgeDays,
    IReadOnlyList<AllergyFact> Allergies,
    bool HasRecordedAllergies,
    IReadOnlyList<ActiveMedicineFact> ActiveMedicines,
    IReadOnlyList<string> UnresolvedProfileMedications);

public enum WeightRequirementKind
{
    Satisfied,
    Missing,
    Stale
}

public sealed record WeightRequirement(WeightRequirementKind Kind, decimal? LatestKilograms, DateOnly? RecordedOn, int? AgeDays)
{
    public bool IsSatisfied => Kind == WeightRequirementKind.Satisfied;
}

public sealed record DoseCheck(
    DoseCheckOutcome Outcome,
    decimal? DoseMg,
    decimal? DoseMgPerKg,
    decimal? ExpectedMinMgPerKg,
    decimal? ExpectedMaxMgPerKg,
    decimal? ExpectedMinMg,
    decimal? ExpectedMaxMg);

public sealed record SafetyWarning(
    SafetyWarningKind Kind,
    bool Blocking,
    bool RequiresAcknowledgement,
    InteractionSeverity? Severity,
    string Subject,
    string Detail);

public sealed record SafetyAssessment(
    WeightRequirement Weight,
    DoseCheck DoseCheck,
    IReadOnlyList<SafetyWarning> Warnings,
    IReadOnlyList<string> Information,
    string Contraindications,
    bool HasRecordedAllergies,
    string Fingerprint)
{
    public IReadOnlyList<SafetyWarning> NeedingAcknowledgement => Warnings.Where(w => w.RequiresAcknowledgement).ToList();
    public bool RequiresAcknowledgement => Warnings.Any(w => w.RequiresAcknowledgement);
    public bool HasAllergyConflict => Warnings.Any(w => w.Kind == SafetyWarningKind.AllergyConflict);
}

/// <summary>What the vet sent back with the issue command (§8.5 "Acknowledgement is bound to what was seen").</summary>
public sealed record SafetyAcknowledgementInput(string? Fingerprint, string? AllergyReason, string? DoseNote);

public enum AcknowledgementResult
{
    /// <summary>Nothing needed acknowledging, or everything was, against the same warnings.</summary>
    Accepted,

    /// <summary>Something needs acknowledging and no fingerprint was sent -- 400 <c>PrescriptionWarningsNotAcknowledged</c>.</summary>
    NotAcknowledged,

    /// <summary>A fingerprint was sent and differs from the current one -- 409 <c>PrescriptionWarningsChanged</c>.</summary>
    Changed,

    /// <summary>An allergy conflict was acknowledged without a reason -- 400 <c>AllergyAcknowledgementReasonRequired</c>.</summary>
    AllergyReasonRequired
}

/// <summary>HO-PHM-001 §8.3 -- everything <c>PrescriptionAggregate.Issue</c> needs. The handler supplies the cross-aggregate facts; the aggregate decides.</summary>
public sealed record PrescriptionIssueDto(
    string PrescriptionNumber,
    Guid BranchId,
    Guid CatId,
    Guid CustomerId,
    Guid VisitId,
    bool IsRemoteRenewal,
    Guid PrescriberId,
    FormularyMedicineSnapshot Medicine,
    Dose Dose,
    AdministrationRoute Route,
    DoseFrequency Frequency,
    int DurationDays,
    decimal Quantity,
    string Instructions,
    DateOnly StartsOn,
    bool IsRepeatable,
    int RepeatsAuthorised,
    DateOnly? ValidUntil,
    DateOnly Today,
    int DefaultDispenseWindowDays,
    int MaxRepeatValidityDays,
    SafetyAssessment Assessment,
    SafetyAcknowledgementInput Acknowledgement,
    DateTimeOffset Now,
    Guid? RenewedFromPrescriptionId,
    PharmacyActor Actor);

public sealed record RepeatRequestDto(
    Guid RequestId, Guid RequestedBy, Guid PreferredBranchId, string? Note, DateOnly Today, DateTimeOffset Now, PharmacyActor Actor);
