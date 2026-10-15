using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>Why a cat is blocked: a core vaccine never given, or due before the departure date.</summary>
public sealed record GateReason(string VaccineCode, string VaccineName, string Reason, DateOnly? DueOn)
{
    public const string DueBeforeDeparture = nameof(DueBeforeDeparture);
}

public sealed record GateEvaluation(bool Passed, IReadOnlyList<GateReason> Reasons, DateOnly EvaluatedOn);

/// <summary>
/// HO-BRD-001 section 8.6 (BR-1, ADR-222, renumbered ADR-355). Pure. A cat passes when the shared <see cref="VaccinationStatusRule"/>, evaluated as of
/// the DEPARTURE date, has no core vaccine due ON OR BEFORE the last day of the stay (owner decision 2026-10-15). At booking time this blocks every cat the story calls Overdue
/// (US-BRD-04 AC1) and a cat whose vaccine would lapse mid-stay (OQ-8).
/// </summary>
public static class BoardingVaccinationGate
{
    public static GateEvaluation Evaluate(VaccinationEvaluation evaluationAtDeparture, DateOnly departureOn)
    {
        // "On or before" (owner decision 2026-10-15, section 8.6): a vaccine due ON the last day of the stay blocks too. The shared rule is strict (Overdue means due BEFORE the
        // date, HO-CAT-001 Q4), so the boundary day is added here and the cat-profile status is untouched.
        var reasons = evaluationAtDeparture.Items
            .Where(i => i.Status == VaccinationStatus.Overdue || (i.NextDueOn is { } due && due <= departureOn))
            .Select(i => new GateReason(i.Code, i.Name, i.Reason ?? GateReason.DueBeforeDeparture, i.NextDueOn))
            .ToList();

        return new GateEvaluation(reasons.Count == 0, reasons, departureOn);
    }
}
