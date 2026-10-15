using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-PHM-001 §8.3 / §8.5 -- what the vet saw and acknowledged at issue: the weight used, the dose in mg/kg and the range it was compared with,
/// the outcome, the fingerprint of the warnings that needed acknowledging, and who acknowledged them and why. Kept forever with the prescription.
/// </summary>
public sealed record SafetyRecord
{
    public decimal? WeightKg { get; init; }
    public DateOnly? WeightRecordedOn { get; init; }
    public decimal? DoseMgPerKg { get; init; }
    public decimal? ExpectedMinMgPerKg { get; init; }
    public decimal? ExpectedMaxMgPerKg { get; init; }
    public DoseCheckOutcome DoseCheckOutcome { get; init; }
    public string WarningsFingerprint { get; init; }
    public Guid? AcknowledgedBy { get; init; }
    public DateTimeOffset? AcknowledgedOn { get; init; }
    public string? AllergyAcknowledgementReason { get; init; }
    public string? DoseAcknowledgementNote { get; init; }

    public SafetyRecord(
        decimal? weightKg, DateOnly? weightRecordedOn, decimal? doseMgPerKg, decimal? expectedMinMgPerKg, decimal? expectedMaxMgPerKg,
        DoseCheckOutcome doseCheckOutcome, string warningsFingerprint, Guid? acknowledgedBy, DateTimeOffset? acknowledgedOn,
        string? allergyAcknowledgementReason, string? doseAcknowledgementNote)
    {
        WeightKg = weightKg;
        WeightRecordedOn = weightRecordedOn;
        DoseMgPerKg = doseMgPerKg;
        ExpectedMinMgPerKg = expectedMinMgPerKg;
        ExpectedMaxMgPerKg = expectedMaxMgPerKg;
        DoseCheckOutcome = doseCheckOutcome;
        WarningsFingerprint = warningsFingerprint;
        AcknowledgedBy = acknowledgedBy;
        AcknowledgedOn = acknowledgedOn;
        AllergyAcknowledgementReason = allergyAcknowledgementReason;
        DoseAcknowledgementNote = doseAcknowledgementNote;
    }

    // EF only.
    private SafetyRecord()
    {
        DoseCheckOutcome = default!;
        WarningsFingerprint = default!;
    }
}
