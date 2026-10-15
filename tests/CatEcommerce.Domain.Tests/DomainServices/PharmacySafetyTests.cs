using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-PHM-001 §15 groups A (U-A1, U-A2) and C (U-C1 ... U-C16) -- the pure safety rules (ADR-139 ... ADR-141).</summary>
public sealed class PharmacySafetyTests
{
    private static readonly DateOnly Today = new(2026, 10, 9);

    private static FormularyMedicineSnapshot Medicine(
        string name = "Meloxicam", Strength? strength = null, DosingBasis? basis = null, DoseRange? range = null, string[]? ingredients = null, string[]? classes = null,
        InteractionSnapshot[]? interactions = null, Guid? id = null) => new(
        id ?? Guid.NewGuid(), "MEL", name, MedicineForm.OralLiquid, strength ?? new Strength(1.5m, StrengthUnit.Mg, 1m, DispensingUnit.Millilitre),
        AdministrationRoute.Oral, [AdministrationRoute.Oral], basis ?? DosingBasis.PerKg, range ?? new DoseRange(0.05m, 0.1m), "None", ingredients ?? ["meloxicam"],
        classes ?? ["nsaid"], interactions ?? [], false, null, DispensingUnit.Millilitre, [], true);

    private static SafetyInput Input(
        FormularyMedicineSnapshot? medicine = null, Dose? dose = null, LatestWeight? weight = null, bool noWeight = false, AllergyFact[]? allergies = null,
        bool hasAllergies = true, ActiveMedicineFact[]? active = null, string[]? unresolved = null) => new(
        medicine ?? Medicine(), dose ?? new Dose(0.2m, DoseUnit.Ml), AdministrationRoute.Oral, DoseFrequency.OnceDaily, "Mitu",
        noWeight ? null : weight ?? new LatestWeight(4.00m, Today), Today, 30, allergies ?? [], hasAllergies, active ?? [], unresolved ?? []);

    private static ActiveMedicineFact Active(
        string name, string[]? ingredients = null, string[]? classes = null, InteractionSnapshot[]? interactions = null, Guid? id = null,
        ActiveMedicineSource source = ActiveMedicineSource.Prescription, string? number = "RX-1") => new(
        id, name, ingredients ?? [], classes ?? [], interactions ?? [], source, number);

    // ── U-A1 ────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Penicillins (Amoxicillin)", "penicillin amoxicillin")]
    [InlineData("GAS", "gas")]
    [InlineData("Méloxicam", "meloxicam")]
    public void U_A1_TheNormaliser_FoldsCaseAccentsPunctuationAndPlurals(string text, string expected) =>
        Assert.Equal(expected, MedicineTermNormaliser.Normalise(text));

    // ── U-A2 ────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    // status, startsOn offset, lastDispensed offset (null none), duration, repeats, supplies, validUntil offset, expectedActive
    [InlineData("Issued", 0, null, 5, 0, 0, 30, true)]          // issued today: the course starts now
    [InlineData("Dispensed", -3, -3, 5, 2, 1, 30, true)]        // mid-course
    [InlineData("Completed", -3, -3, 5, 0, 1, 30, true)]        // completed but the course still runs
    [InlineData("Completed", -20, -20, 5, 0, 1, 30, false)]     // completed and over
    [InlineData("Dispensed", -40, -40, 5, 2, 1, -10, false)]    // past ValidUntil with supplies left: inactive
    [InlineData("Dispensed", -40, -40, 5, 2, 1, 10, true)]      // supplies left and still valid: a refill is due
    [InlineData("Cancelled", 0, null, 5, 0, 0, 30, false)]
    [InlineData("Expired", -40, -40, 5, 2, 1, -5, false)]
    public void U_A2_ActivityRules_FollowThePrescriptionLifecycle(
        string status, int startsOffset, int? lastOffset, int duration, int repeats, int supplies, int validOffset, bool expectedActive)
    {
        var active = PrescriptionActivityRules.IsActive(
            Enumeration.FromDisplayName<PrescriptionStatus>(status), Today.AddDays(startsOffset), lastOffset is { } l ? Today.AddDays(l) : null, duration, repeats, supplies,
            Today.AddDays(validOffset), Today);

        Assert.Equal(expectedActive, active);
    }

    [Fact]
    public void U_A2_RemainingDays_CountsFromTheLastSupply()
    {
        Assert.Equal(2, PrescriptionActivityRules.RemainingDays(Today.AddDays(-10), Today.AddDays(-3), 5, Today));
        Assert.Equal(0, PrescriptionActivityRules.RemainingDays(Today.AddDays(-10), Today.AddDays(-9), 5, Today));
    }

    // ── U-C1 ... U-C3 ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_C1_MeloxicamAtFourKilograms_PointTwoMl_IsWithinRange_WithTheExpectedWindow()
    {
        var assessment = PrescriptionSafetyAssessor.Assess(Input());

        Assert.Equal(DoseCheckOutcome.WithinRange, assessment.DoseCheck.Outcome);
        Assert.Equal(0.3m, assessment.DoseCheck.DoseMg);
        Assert.Equal(0.075m, assessment.DoseCheck.DoseMgPerKg);
        Assert.Equal(0.2m, assessment.DoseCheck.ExpectedMinMg);
        Assert.Equal(0.4m, assessment.DoseCheck.ExpectedMaxMg);
        Assert.False(assessment.RequiresAcknowledgement);
    }

    [Fact]
    public void U_C2_PointFourMl_IsAboveRange_AndTheWarningNeedsAcknowledging()
    {
        var assessment = PrescriptionSafetyAssessor.Assess(Input(dose: new Dose(0.4m, DoseUnit.Ml)));

        Assert.Equal(DoseCheckOutcome.AboveRange, assessment.DoseCheck.Outcome);
        Assert.Equal(0.15m, assessment.DoseCheck.DoseMgPerKg);
        var warning = Assert.Single(assessment.Warnings);
        Assert.Equal(SafetyWarningKind.DoseAboveRange, warning.Kind);
        Assert.True(warning.RequiresAcknowledgement);
        Assert.True(assessment.RequiresAcknowledgement);
    }

    [Fact]
    public void U_C2b_BelowRange_AlsoNeedsAcknowledging()
    {
        var assessment = PrescriptionSafetyAssessor.Assess(Input(dose: new Dose(0.05m, DoseUnit.Ml)));

        Assert.Equal(DoseCheckOutcome.BelowRange, assessment.DoseCheck.Outcome);
        Assert.Equal(SafetyWarningKind.DoseBelowRange, Assert.Single(assessment.Warnings).Kind);
    }

    [Theory]
    [InlineData(0.1, "WithinRange")] // 0.15 mg / 3 kg = 0.05 mg/kg: exactly the minimum
    [InlineData(0.2, "WithinRange")] // 0.30 mg / 3 kg = 0.10 mg/kg: exactly the maximum
    public void U_C3_ADoseExactlyAtTheMinimumAndTheMaximum_IsWithinRange(double ml, string outcome)
    {
        var assessment = PrescriptionSafetyAssessor.Assess(Input(dose: new Dose((decimal)ml, DoseUnit.Ml), weight: new LatestWeight(3.00m, Today)));

        Assert.Equal(outcome, assessment.DoseCheck.Outcome.DisplayName);
        Assert.Empty(assessment.Warnings);
    }

    // ── U-C4 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_C4_ADoseInDrops_AStrengthInIu_AndATabletAgainstAPerMlStrength_AreNotCheckable_WithAWarningThatNeedsNoAcknowledgement()
    {
        var iu = Medicine(strength: new Strength(100m, StrengthUnit.Iu, 1m, DispensingUnit.Millilitre));
        var cases = new[]
        {
            Input(dose: new Dose(3m, DoseUnit.Drop)),
            Input(medicine: iu, dose: new Dose(0.2m, DoseUnit.Ml)),
            Input(dose: new Dose(1m, DoseUnit.Tablet)),
        };

        foreach (var input in cases)
        {
            var assessment = PrescriptionSafetyAssessor.Assess(input);
            Assert.Equal(DoseCheckOutcome.NotCheckable, assessment.DoseCheck.Outcome);
            var warning = Assert.Single(assessment.Warnings);
            Assert.Equal(SafetyWarningKind.DoseNotCheckable, warning.Kind);
            Assert.False(warning.RequiresAcknowledgement);
            Assert.False(assessment.RequiresAcknowledgement);
        }
    }

    // ── U-C5 / U-C6 ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_C5_APerKgMedicineNeedsARecentWeight_MissingStaleAndSatisfied()
    {
        var missing = PrescriptionSafetyAssessor.Assess(Input(noWeight: true));
        Assert.Equal(WeightRequirementKind.Missing, missing.Weight.Kind);

        var stale = PrescriptionSafetyAssessor.Assess(Input(weight: new LatestWeight(4m, Today.AddDays(-31))));
        Assert.Equal(WeightRequirementKind.Stale, stale.Weight.Kind);
        Assert.Equal(31, stale.Weight.AgeDays);

        var fresh = PrescriptionSafetyAssessor.Assess(Input(weight: new LatestWeight(4m, Today.AddDays(-30))));
        Assert.Equal(WeightRequirementKind.Satisfied, fresh.Weight.Kind);
    }

    [Fact]
    public void U_C6_AFixedMedicine_NeverNeedsAWeight()
    {
        var assessment = PrescriptionSafetyAssessor.Assess(Input(medicine: Medicine(basis: DosingBasis.Fixed), noWeight: true));

        Assert.Equal(WeightRequirementKind.Satisfied, assessment.Weight.Kind);
    }

    // ── U-C7 ... U-C9 ───────────────────────────────────────────────────────────────────────────

    private static FormularyMedicineSnapshot Amoxicillin() => Medicine("Amoxicillin", ingredients: ["amoxicillin"], classes: ["penicillin"], basis: DosingBasis.Fixed);

    [Fact]
    public void U_C7_APenicillinAllergy_AgainstAmoxicillin_IsABlockingConflictThatNeedsAReason()
    {
        var assessment = PrescriptionSafetyAssessor.Assess(Input(medicine: Amoxicillin(), allergies: [new AllergyFact("Penicillin", "Rash", AllergySeverity.Severe)]));

        var warning = Assert.Single(assessment.Warnings, w => w.Kind == SafetyWarningKind.AllergyConflict);
        Assert.True(warning.Blocking);
        Assert.True(warning.RequiresAcknowledgement);
        Assert.True(assessment.HasAllergyConflict);

        // Acknowledging needs the fingerprint AND a reason.
        Assert.Equal(AcknowledgementResult.NotAcknowledged, SafetyAcknowledgement.Evaluate(assessment, new SafetyAcknowledgementInput(null, null, null)));
        Assert.Equal(AcknowledgementResult.AllergyReasonRequired, SafetyAcknowledgement.Evaluate(assessment, new SafetyAcknowledgementInput(assessment.Fingerprint, " ", null)));
        Assert.Equal(AcknowledgementResult.Changed, SafetyAcknowledgement.Evaluate(assessment, new SafetyAcknowledgementInput("stale", "Benefit outweighs risk", null)));
        Assert.Equal(AcknowledgementResult.Accepted, SafetyAcknowledgement.Evaluate(assessment, new SafetyAcknowledgementInput(assessment.Fingerprint, "Benefit outweighs risk", null)));
    }

    [Theory]
    [InlineData("Pollen")]
    [InlineData("chicken")]
    [InlineData("pen")]
    public void U_C8_UnrelatedOrTooShortAllergens_AreNotConflicts(string allergen)
    {
        var assessment = PrescriptionSafetyAssessor.Assess(Input(medicine: Amoxicillin(), allergies: [new AllergyFact(allergen, "Rash", AllergySeverity.Mild)]));

        Assert.False(assessment.HasAllergyConflict);
    }

    [Fact]
    public void U_C9_AMisspeltAllergen_IsNotMatched_TheDocumentedLimitation()
    {
        var assessment = PrescriptionSafetyAssessor.Assess(Input(medicine: Amoxicillin(), allergies: [new AllergyFact("penicilin", "Rash", AllergySeverity.Severe)]));

        Assert.False(assessment.HasAllergyConflict);
    }

    // ── U-C10 ... U-C13 ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_C10_TheInteractionIsFoundInBothDirections()
    {
        var prednisolone = Medicine("Prednisolone", ingredients: ["prednisolone"], classes: ["corticosteroid"], basis: DosingBasis.Fixed);
        var meloxicam = Active("Meloxicam", ["meloxicam"], ["nsaid"], [new InteractionSnapshot(null, "corticosteroid", InteractionSeverity.Major, "GI ulceration risk")], Guid.NewGuid());

        var assessment = PrescriptionSafetyAssessor.Assess(Input(medicine: prednisolone, active: [meloxicam]));

        var warning = Assert.Single(assessment.Warnings, w => w.Kind == SafetyWarningKind.Interaction);
        Assert.Equal(InteractionSeverity.Major, warning.Severity);
        Assert.True(warning.RequiresAcknowledgement);
        Assert.Contains("GI ulceration", warning.Detail);
    }

    [Fact]
    public void U_C11_AnInteractionListedOnBothSides_IsOneWarning_AtTheHigherSeverity_WithBothNotes()
    {
        var newMedicine = Medicine("Prednisolone", classes: ["corticosteroid"], basis: DosingBasis.Fixed, interactions: [new InteractionSnapshot(null, "nsaid", InteractionSeverity.Moderate, "Note from prednisolone")]);
        var meloxicam = Active("Meloxicam", classes: ["nsaid"], interactions: [new InteractionSnapshot(null, "corticosteroid", InteractionSeverity.Major, "Note from meloxicam")]);

        var warning = Assert.Single(PrescriptionSafetyAssessor.Assess(Input(medicine: newMedicine, active: [meloxicam])).Warnings, w => w.Kind == SafetyWarningKind.Interaction);

        Assert.Equal(InteractionSeverity.Major, warning.Severity);
        Assert.Contains("Note from prednisolone", warning.Detail);
        Assert.Contains("Note from meloxicam", warning.Detail);
    }

    [Fact]
    public void U_C12_AMinorInteraction_IsShownWithItsSeverity_ButNeedsNoAcknowledgement()
    {
        var newMedicine = Medicine("Gabapentin", classes: ["anticonvulsant"], basis: DosingBasis.Fixed, interactions: [new InteractionSnapshot(null, "nsaid", InteractionSeverity.Minor, "Minor note")]);

        var assessment = PrescriptionSafetyAssessor.Assess(Input(medicine: newMedicine, active: [Active("Meloxicam", classes: ["nsaid"])]));

        var warning = Assert.Single(assessment.Warnings, w => w.Kind == SafetyWarningKind.Interaction);
        Assert.Equal(InteractionSeverity.Minor, warning.Severity);
        Assert.False(warning.RequiresAcknowledgement);
        Assert.False(assessment.RequiresAcknowledgement);
    }

    [Fact]
    public void U_C13_TheSameMedicineAlreadyActive_IsAMajorInteraction_NamingTheExistingPrescription()
    {
        var id = Guid.NewGuid();
        var assessment = PrescriptionSafetyAssessor.Assess(Input(medicine: Medicine(id: id), active: [Active("Meloxicam", id: id, number: "RX-2026-000042")]));

        var warning = Assert.Single(assessment.Warnings, w => w.Kind == SafetyWarningKind.Interaction);
        Assert.Equal(InteractionSeverity.Major, warning.Severity);
        Assert.Contains("RX-2026-000042", warning.Detail);
    }

    // ── U-C14 ... U-C16 ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_C14_AProfileMedicationThatResolvesIsChecked_OneThatDoesNotIsListedAsInformation()
    {
        var prednisolone = Medicine("Prednisolone", classes: ["corticosteroid"], basis: DosingBasis.Fixed);
        var profile = Active("Meloxicam", classes: ["nsaid"], interactions: [new InteractionSnapshot(null, "corticosteroid", InteractionSeverity.Major, "x")], source: ActiveMedicineSource.ProfileMedication, number: null);

        var assessment = PrescriptionSafetyAssessor.Assess(Input(medicine: prednisolone, active: [profile], unresolved: ["fish oil"]));

        Assert.Single(assessment.Warnings, w => w.Kind == SafetyWarningKind.Interaction);
        Assert.Contains(assessment.Information, i => i.Contains("fish oil"));
    }

    [Fact]
    public void U_C15_TheFingerprint_IsStableInAnyOrder_AndChangesWhenTheWeightDateChanges()
    {
        var newMedicine = Medicine("Prednisolone", ingredients: ["prednisolone"], classes: ["corticosteroid"], basis: DosingBasis.Fixed);
        var a = Active("Meloxicam", classes: ["nsaid"], interactions: [new InteractionSnapshot(null, "corticosteroid", InteractionSeverity.Major, "x")]);
        var b = Active("Aspirin", classes: ["nsaid"], interactions: [new InteractionSnapshot(null, "corticosteroid", InteractionSeverity.Major, "y")]);

        var first = PrescriptionSafetyAssessor.Assess(Input(medicine: newMedicine, active: [a, b]));
        var second = PrescriptionSafetyAssessor.Assess(Input(medicine: newMedicine, active: [b, a]));
        Assert.Equal(first.Fingerprint, second.Fingerprint);

        var above1 = PrescriptionSafetyAssessor.Assess(Input(dose: new Dose(0.4m, DoseUnit.Ml), weight: new LatestWeight(4m, Today)));
        var above2 = PrescriptionSafetyAssessor.Assess(Input(dose: new Dose(0.4m, DoseUnit.Ml), weight: new LatestWeight(4m, Today.AddDays(-1))));
        Assert.NotEqual(above1.Fingerprint, above2.Fingerprint);
    }

    [Fact]
    public void U_C16_NoRecordedAllergies_IsInformation_NotAWarning()
    {
        var assessment = PrescriptionSafetyAssessor.Assess(Input(hasAllergies: false));

        Assert.Empty(assessment.Warnings);
        Assert.Contains(PharmacyMessages.NoAllergiesRecorded, assessment.Information);
        Assert.False(assessment.HasRecordedAllergies);
    }
}
