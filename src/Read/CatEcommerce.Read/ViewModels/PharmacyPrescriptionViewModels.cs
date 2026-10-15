using System.Globalization;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Read.ViewModels;

// HO-PHM-001 §12.4. Every quantity, dose and weight carries its unit (UI-8); instants are ISO-8601 with offset; dates are yyyy-MM-dd.

public sealed record PrescriptionMedicineViewModel(string Name, string Strength, string Form, bool IsControlled);

/// <summary>A prescription as listed: staff lists, the visit list, the cat history and the pharmacy queue all use it. <c>Status</c> is the EFFECTIVE status
/// (<c>Expired</c> once <c>ValidUntil</c> has passed with supplies left, whether or not the sweep has run, ADR-142); <c>IsActive</c> is derived by
/// <c>PrescriptionActivityRules</c>, never stored.</summary>
public record PrescriptionSummaryViewModel(
    Guid Id,
    string PrescriptionNumber,
    PrescriptionMedicineViewModel Medicine,
    QuantityViewModel Dose,
    string Route,
    string Frequency,
    int DurationDays,
    QuantityViewModel Quantity,
    string Status,
    bool IsActive,
    DateOnly CourseEndsOn,
    int RemainingDays,
    int SuppliesDispensed,
    int SuppliesAuthorised,
    DateOnly ValidUntil,
    string PrescriberName,
    DateTimeOffset IssuedOn,
    bool HasAcknowledgedWarnings);

public sealed record ExpectedRangeViewModel(string MinMgPerKg, string MaxMgPerKg, QuantityViewModel? MinMg, QuantityViewModel? MaxMg);

public sealed record SafetyRecordViewModel(
    QuantityViewModel? WeightUsed,
    DateOnly? WeightRecordedOn,
    string? DoseMgPerKg,
    ExpectedRangeViewModel? ExpectedRange,
    string DoseCheckOutcome,
    string? AcknowledgedByName,
    DateTimeOffset? AcknowledgedOn,
    string? AllergyAcknowledgementReason,
    string? DoseAcknowledgementNote);

public sealed record PrescriptionWarningViewModel(string Kind, bool Blocking, bool RequiresAcknowledgement, string? Severity, string Subject, string Detail);

public sealed record RepeatRequestViewModel(
    Guid Id, DateTimeOffset RequestedOn, Guid PreferredBranchId, string? Note, string Status, DateTimeOffset? DecidedOn, string? DeclineReason, Guid? DispensingId);

public sealed record PrescriptionDispensingLineViewModel(Guid LineId, string LotNumber, DateOnly? ExpiresOn, QuantityViewModel Quantity);

public sealed record PrescriptionDispensingViewModel(
    Guid Id, int SupplyNumber, bool IsRepeat, DateOnly DispensedOn, string DispensedByName, string Status, IReadOnlyList<PrescriptionDispensingLineViewModel> Lines);

public sealed record PrescriptionDetailViewModel(
    Guid Id,
    string PrescriptionNumber,
    PrescriptionMedicineViewModel Medicine,
    QuantityViewModel Dose,
    string Route,
    string Frequency,
    int DurationDays,
    QuantityViewModel Quantity,
    string Status,
    bool IsActive,
    DateOnly CourseEndsOn,
    int RemainingDays,
    int SuppliesDispensed,
    int SuppliesAuthorised,
    DateOnly ValidUntil,
    string PrescriberName,
    DateTimeOffset IssuedOn,
    bool HasAcknowledgedWarnings,
    Guid CatId,
    string CatName,
    string Instructions,
    Guid VisitId,
    bool IsRemoteRenewal,
    DateOnly StartsOn,
    bool IsRepeatable,
    SafetyRecordViewModel SafetyRecord,
    IReadOnlyList<PrescriptionWarningViewModel> Warnings,
    IReadOnlyList<PrescriptionDispensingViewModel> Dispensings,
    IReadOnlyList<RepeatRequestViewModel> RepeatRequests,
    PrescriptionReferenceViewModel? RenewedFrom,
    PrescriptionReferenceViewModel? RenewedBy,
    IReadOnlyList<string> AllowedActions)
    : PrescriptionSummaryViewModel(
        Id, PrescriptionNumber, Medicine, Dose, Route, Frequency, DurationDays, Quantity, Status, IsActive, CourseEndsOn, RemainingDays, SuppliesDispensed,
        SuppliesAuthorised, ValidUntil, PrescriberName, IssuedOn, HasAcknowledgedWarnings);

public sealed record PrescriptionReferenceViewModel(Guid Id, string PrescriptionNumber);

// ── the safety check (US-PHM-02, US-PHM-03) ─────────────────────────────────────────────────────────────────────────────────────

public sealed record LatestWeightViewModel(QuantityViewModel Weight, DateOnly RecordedOn, int AgeDays);

public sealed record WeightRequirementViewModel(string Requirement, LatestWeightViewModel? Latest);

public sealed record DoseCheckViewModel(string Outcome, QuantityViewModel? DoseMg, string? DoseMgPerKg, ExpectedRangeViewModel? ExpectedRange);

public sealed record SafetyWarningViewModel(string Kind, bool Blocking, bool RequiresAcknowledgement, string? Severity, string Subject, string Detail);

/// <summary>The server's assessment of a prescription to be (§12.4): nothing here is written. <c>Fingerprint</c> is what the issue command sends back to
/// say "I have seen exactly these warnings".</summary>
public sealed record SafetyAssessmentViewModel(
    WeightRequirementViewModel Weight,
    DoseCheckViewModel DoseCheck,
    IReadOnlyList<SafetyWarningViewModel> Warnings,
    IReadOnlyList<string> Information,
    string Contraindications,
    bool HasRecordedAllergies,
    string Fingerprint)
{
    public static SafetyAssessmentViewModel From(SafetyAssessment assessment)
    {
        static string Number(decimal value) => value.ToString("0.00##", CultureInfo.InvariantCulture);

        var weight = assessment.Weight;
        var check = assessment.DoseCheck;

        ExpectedRangeViewModel? range = check.ExpectedMinMgPerKg is { } min && check.ExpectedMaxMgPerKg is { } max
            ? new ExpectedRangeViewModel(
                Number(min), Number(max),
                check.ExpectedMinMg is { } minMg ? QuantityViewModel.Of(minMg, "Mg") : null, check.ExpectedMaxMg is { } maxMg ? QuantityViewModel.Of(maxMg, "Mg") : null)
            : null;

        return new SafetyAssessmentViewModel(
            new WeightRequirementViewModel(
                weight.Kind.ToString(),
                weight.LatestKilograms is { } kg && weight.RecordedOn is { } on ? new LatestWeightViewModel(QuantityViewModel.Of(kg, "Kg"), on, weight.AgeDays ?? 0) : null),
            new DoseCheckViewModel(
                check.Outcome.DisplayName, check.DoseMg is { } mg ? QuantityViewModel.Of(mg, "Mg") : null, check.DoseMgPerKg is { } perKg ? Number(perKg) : null, range),
            assessment.Warnings.Select(w => new SafetyWarningViewModel(w.Kind.DisplayName, w.Blocking, w.RequiresAcknowledgement, w.Severity?.DisplayName, w.Subject, w.Detail)).ToList(),
            assessment.Information, assessment.Contraindications, assessment.HasRecordedAllergies, assessment.Fingerprint);
    }
}
