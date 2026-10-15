using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-PHM-001 §8.5 (ADR-139) -- THE safety assessment: dose against the formulary mg/kg range for the cat's latest weight, allergy conflicts and
/// interactions with what the cat is already taking. One pure function, run twice -- by the check query for the screen and again inside the issue
/// command -- so an acknowledgement cannot be skipped by calling the API directly. It writes nothing and reads nothing; the caller loads
/// <see cref="SafetyInput"/>.
///
/// What it decides: the weight requirement (a per-kg medicine needs a weight no older than the window, with no acknowledgement path); the dose
/// check (<see cref="DoseConverter"/>); which warnings exist, which are blocking and which need acknowledging; and the fingerprint that binds an
/// acknowledgement to exactly the warnings the vet saw.
/// </summary>
public static class PrescriptionSafetyAssessor
{
    public static SafetyAssessment Assess(SafetyInput input)
    {
        var medicine = input.Medicine;
        var weight = AssessWeight(input);
        var doseCheck = AssessDose(input, weight);

        var warnings = new List<SafetyWarning>();
        AddDoseWarning(warnings, input, weight, doseCheck);

        foreach (var conflict in AllergyConflictDetector.Detect(medicine, input.Allergies))
        {
            warnings.Add(new SafetyWarning(
                SafetyWarningKind.AllergyConflict, Blocking: true, RequiresAcknowledgement: true, Severity: null, Subject: Truncate(conflict.Allergen, PrescriptionRules.MaxWarningSubjectLength),
                Detail: Truncate($"Recorded allergy '{conflict.Allergen}' ({conflict.Reaction}, {conflict.Severity.DisplayName}) matches '{conflict.MatchedTerm}' in this medicine.", PrescriptionRules.MaxWarningDetailLength)));
        }

        foreach (var interaction in InteractionDetector.Detect(medicine, input.ActiveMedicines, input.CatName))
        {
            warnings.Add(new SafetyWarning(
                SafetyWarningKind.Interaction, Blocking: false, RequiresAcknowledgement: interaction.Severity.RequiresAcknowledgement, Severity: interaction.Severity,
                Subject: Truncate(interaction.OtherName, PrescriptionRules.MaxWarningSubjectLength),
                Detail: Truncate($"{interaction.Severity.DisplayName}: {interaction.Notes}", PrescriptionRules.MaxWarningDetailLength)));
        }

        var information = new List<string>();
        if (!input.HasRecordedAllergies) information.Add(PharmacyMessages.NoAllergiesRecorded);
        information.AddRange(input.UnresolvedProfileMedications.Select(name => $"Also taking: '{name}' - not in the formulary, not checked."));

        var fingerprint = Fingerprint(warnings, doseCheck, weight);

        return new SafetyAssessment(weight, doseCheck, warnings, information, medicine.Contraindications, input.HasRecordedAllergies, fingerprint);
    }

    private static WeightRequirement AssessWeight(SafetyInput input)
    {
        var latest = input.LatestWeight;
        if (input.Medicine.DosingBasis == DosingBasis.Fixed)
            return new WeightRequirement(WeightRequirementKind.Satisfied, latest?.Kilograms, latest?.RecordedOn, latest is null ? null : AgeDays(input.Today, latest.RecordedOn));

        if (latest is null) return new WeightRequirement(WeightRequirementKind.Missing, null, null, null);

        var age = AgeDays(input.Today, latest.RecordedOn);
        return age > input.WeightMaxAgeDays
            ? new WeightRequirement(WeightRequirementKind.Stale, latest.Kilograms, latest.RecordedOn, age)
            : new WeightRequirement(WeightRequirementKind.Satisfied, latest.Kilograms, latest.RecordedOn, age);
    }

    private static int AgeDays(DateOnly today, DateOnly recordedOn) => Math.Max(0, today.DayNumber - recordedOn.DayNumber);

    private static DoseCheck AssessDose(SafetyInput input, WeightRequirement weight)
    {
        var doseMg = DoseConverter.ToMilligrams(input.Dose, input.Medicine.Strength);
        var range = input.Medicine.DoseRange;

        if (input.Medicine.DosingBasis != DosingBasis.PerKg || range is null || doseMg is null || !weight.IsSatisfied || input.LatestWeight is null)
            return new DoseCheck(DoseCheckOutcome.NotCheckable, doseMg, null, range?.MinMgPerKg, range?.MaxMgPerKg, null, null);

        var kg = input.LatestWeight.Kilograms;
        var mgPerKg = DoseConverter.MgPerKg(doseMg.Value, kg);
        var min = DoseConverter.Round(range.MinMgPerKg);
        var max = DoseConverter.Round(range.MaxMgPerKg);
        var outcome = mgPerKg < min ? DoseCheckOutcome.BelowRange : mgPerKg > max ? DoseCheckOutcome.AboveRange : DoseCheckOutcome.WithinRange;

        return new DoseCheck(outcome, DoseConverter.Round(doseMg.Value), mgPerKg, min, max, DoseConverter.Round(min * kg), DoseConverter.Round(max * kg));
    }

    private static void AddDoseWarning(List<SafetyWarning> warnings, SafetyInput input, WeightRequirement weight, DoseCheck dose)
    {
        string Range() =>
            $"{Format(dose.ExpectedMinMgPerKg)}-{Format(dose.ExpectedMaxMgPerKg)} mg/kg ({Format(dose.ExpectedMinMg)}-{Format(dose.ExpectedMaxMg)} mg for {Format(input.LatestWeight?.Kilograms)} kg)";

        if (dose.Outcome == DoseCheckOutcome.BelowRange)
        {
            warnings.Add(new SafetyWarning(SafetyWarningKind.DoseBelowRange, Blocking: false, RequiresAcknowledgement: true, Severity: null, Subject: "Dose",
                Detail: $"{Format(dose.DoseMgPerKg)} mg/kg is below the expected range {Range()}."));
        }
        else if (dose.Outcome == DoseCheckOutcome.AboveRange)
        {
            warnings.Add(new SafetyWarning(SafetyWarningKind.DoseAboveRange, Blocking: false, RequiresAcknowledgement: true, Severity: null, Subject: "Dose",
                Detail: $"{Format(dose.DoseMgPerKg)} mg/kg is above the expected range {Range()}."));
        }
        else if (dose.Outcome == DoseCheckOutcome.NotCheckable)
        {
            // A per-kg medicine with no usable weight is refused outright (RecentWeightRequired); the missing-weight message says it better than a warning.
            var refusedForWeight = input.Medicine.DosingBasis == DosingBasis.PerKg && !weight.IsSatisfied;
            if (!refusedForWeight)
            {
                warnings.Add(new SafetyWarning(SafetyWarningKind.DoseNotCheckable, Blocking: false, RequiresAcknowledgement: false, Severity: null, Subject: "Dose",
                    Detail: PharmacyMessages.DoseNotCheckable));
            }
        }
    }

    /// <summary>Hex SHA-256 over the sorted (kind, subject, severity, rounded mg/kg, weight date) tuples of every warning that needs acknowledging.</summary>
    private static string Fingerprint(IEnumerable<SafetyWarning> warnings, DoseCheck dose, WeightRequirement weight)
    {
        var tuples = warnings
            .Where(w => w.RequiresAcknowledgement)
            .Select(w => string.Join('|',
                w.Kind.DisplayName, w.Subject, w.Severity?.DisplayName ?? "",
                w.Kind == SafetyWarningKind.DoseBelowRange || w.Kind == SafetyWarningKind.DoseAboveRange ? Format(dose.DoseMgPerKg) : "",
                weight.RecordedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""))
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', tuples)))).ToLowerInvariant();
    }

    private static string Format(decimal? value) => value is { } v ? v.ToString("0.####", CultureInfo.InvariantCulture) : "";

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}

/// <summary>
/// HO-PHM-001 §8.5 -- "acknowledgement is bound to what was seen". Decides what the issue command's acknowledgement fields mean against the CURRENT
/// assessment: nothing needing acknowledgement -> accepted (a stray fingerprint is ignored); something does and no fingerprint -> not acknowledged;
/// a fingerprint that differs -> the warnings changed; an allergy conflict without a reason -> a reason is required. Used by the prescribing
/// steps (to build the 400/409 answers) and again by <c>PrescriptionAggregate.Issue</c> (so the rule cannot be bypassed). Pure.
/// </summary>
public static class SafetyAcknowledgement
{
    public static AcknowledgementResult Evaluate(SafetyAssessment assessment, SafetyAcknowledgementInput input)
    {
        if (!assessment.RequiresAcknowledgement) return AcknowledgementResult.Accepted;
        if (string.IsNullOrWhiteSpace(input.Fingerprint)) return AcknowledgementResult.NotAcknowledged;
        if (!string.Equals(input.Fingerprint.Trim(), assessment.Fingerprint, StringComparison.OrdinalIgnoreCase)) return AcknowledgementResult.Changed;
        if (assessment.HasAllergyConflict && string.IsNullOrWhiteSpace(input.AllergyReason)) return AcknowledgementResult.AllergyReasonRequired;

        return AcknowledgementResult.Accepted;
    }
}
