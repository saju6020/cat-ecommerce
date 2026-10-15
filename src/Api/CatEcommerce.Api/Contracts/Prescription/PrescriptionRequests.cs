namespace CatEcommerce.Api.Contracts.Prescription;

// HO-PHM-001 §12.4 -- request bodies of the prescribing endpoints. The controller mints the prescription id, takes the branch from X-Branch-Id and the caller
// from the session; neither is ever read from a body.

public sealed class DoseRequest
{
    public decimal Amount { get; init; }
    public string Unit { get; init; } = "";
}

/// <summary><c>POST /api/staff/cats/{catId}/prescriptions/safety-check</c> -- the issue fields without the acknowledgements.</summary>
public sealed class SafetyCheckRequest
{
    public Guid MedicineId { get; init; }
    public DoseRequest Dose { get; init; } = new();
    public string Route { get; init; } = "";
    public string Frequency { get; init; } = "";
}

/// <summary>The editable fields shared by issue and renew.</summary>
public class PrescriptionFieldsRequest
{
    public DoseRequest Dose { get; init; } = new();
    public string Route { get; init; } = "";
    public string Frequency { get; init; } = "";
    public int DurationDays { get; init; }
    public decimal Quantity { get; init; }
    public string Instructions { get; init; } = "";
    public DateOnly? StartsOn { get; init; }
    public bool IsRepeatable { get; init; }
    public int RepeatsAuthorised { get; init; }
    public DateOnly? ValidUntil { get; init; }

    /// <summary>The <c>fingerprint</c> of the assessment the vet reviewed -- binds the acknowledgement to those exact warnings (ADR-139).</summary>
    public string? AcknowledgedWarningsFingerprint { get; init; }
    public string? AllergyAcknowledgementReason { get; init; }
    public string? DoseAcknowledgementNote { get; init; }
}

/// <summary><c>POST /api/staff/cats/{catId}/prescriptions</c> (C5).</summary>
public sealed class IssuePrescriptionRequest : PrescriptionFieldsRequest
{
    public Guid VisitId { get; init; }
    public Guid MedicineId { get; init; }
}

/// <summary><c>POST /api/staff/prescriptions/{prescriptionId}/cancel</c> (C6).</summary>
public sealed class CancelPrescriptionRequest
{
    public string Reason { get; init; } = "";
}

/// <summary><c>POST /api/staff/prescriptions/{prescriptionId}/renew</c> (C15): the editable fields of an issue, plus the open visit it is written from -- or none, for a remote renewal.</summary>
public sealed class RenewPrescriptionRequest : PrescriptionFieldsRequest
{
    public Guid? VisitId { get; init; }
}

/// <summary><c>POST /api/staff/prescriptions/{prescriptionId}/repeat-requests/{requestId}/decline</c> (C14).</summary>
public sealed class DeclineRepeatRequestRequest
{
    public string Reason { get; init; } = "";
}

/// <summary><c>POST /api/customers/me/prescriptions/{id}/repeat-requests</c> (C12). The customer is the caller; nothing about who they are is read from the body.</summary>
public sealed class RequestRepeatRequest
{
    public Guid PreferredBranchId { get; init; }
    public string? Note { get; init; }
}
