namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Clinic Appointments (HO-APT-001) is exempted from <c>IPersonalDataEraser</c>
/// registration: it stores ids and a <c>reasonForVisit</c>/<c>presentingComplaint</c>/
/// <c>cancellationReason</c>, which are part of the clinical record and retained under D-8
/// (HO-XCT-001 §8.8, OQ-7). Gate fix (ARCHITECTURE HIGH #4, 2026-09-28): a prior
/// <c>AppointmentsPersonalDataEraser</c> contradicted this design and was removed, along with
/// <c>AppointmentAggregate.RedactPersonalNotes</c>.</summary>
public static partial class ErasureExemptions
{
    private static readonly IReadOnlyList<(string ContextKey, string Reason)> AppointmentsEntries =
    [
        ("appointments", "Ids and a reasonForVisit/presentingComplaint/cancellationReason -- part of the clinical record, retained under D-8 (HO-XCT-001 §8.8, OQ-7)."),
    ];
}
