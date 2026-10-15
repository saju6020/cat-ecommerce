namespace CatEcommerce.Api.Contracts.Visit;

/// <summary>HO-EMR-001 §12.4 -- <c>PUT /api/staff/visits/{visitId}/draft</c>. Plain text only.</summary>
public sealed class SaveConsultationDraftRequest
{
    public string Subjective { get; init; } = "";
    public string Objective { get; init; } = "";
    public string Assessment { get; init; } = "";
    public string Plan { get; init; } = "";
    public string DischargeInstructions { get; init; } = "";
    public IReadOnlyList<Guid> ProblemCodeIds { get; init; } = [];
}

public sealed class ProblemRequest
{
    /// <summary>Optional: an existing problem's id keeps it in place; omitted, the server mints one.</summary>
    public Guid? ProblemId { get; init; }
    public required Guid DiagnosisCodeId { get; init; }
    public required string Certainty { get; init; }
    public string? Note { get; init; }
}

/// <summary>HO-EMR-001 §12.4 -- <c>PUT /api/staff/visits/{visitId}/note</c>.</summary>
public sealed class SaveConsultationNoteRequest
{
    public required int ExpectedVersion { get; init; }
    public string Subjective { get; init; } = "";
    public string Objective { get; init; } = "";
    public string Assessment { get; init; } = "";
    public string Plan { get; init; } = "";
    public string DischargeInstructions { get; init; } = "";
    public IReadOnlyList<ProblemRequest> Problems { get; init; } = [];
    public Guid? AppliedTemplateId { get; init; }
    public int? AppliedTemplateVersion { get; init; }
}

public sealed class FinaliseVisitRequest
{
    public required int ExpectedVersion { get; init; }
}

public sealed class AddVisitAddendumRequest
{
    public required string Text { get; init; }
    public string? Reason { get; init; }
}
