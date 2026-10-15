namespace CatEcommerce.Api.Contracts.Surgery;

/// <summary>HO-SRG-001 section 12.4 request bodies -- the surgical report. A draft may be incomplete; finalisation names whatever is missing.</summary>
public sealed class SaveSurgicalReportDraftRequest
{
    public int? ExpectedVersion { get; init; }
    public string? ProcedurePerformed { get; init; }
    public string? Findings { get; init; }
    public string? Technique { get; init; }
    public string? SutureMaterial { get; init; }
    public bool HadComplications { get; init; }
    public string? Complications { get; init; }
    public required Guid SurgeonId { get; init; }
    public required Guid AssistantId { get; init; }
}

public sealed class FinaliseSurgicalReportRequest
{
    public required int ExpectedVersion { get; init; }
}

public sealed class AddSurgicalReportAddendumRequest
{
    public required string Text { get; init; }
}
