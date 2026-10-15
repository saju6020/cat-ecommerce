namespace CatEcommerce.Api.Contracts.Visit;

/// <summary>HO-EMR-001 §12.4 -- clinical catalogue request bodies.</summary>
public sealed class CreateDiagnosisCodeRequest
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Category { get; init; }
    public string? ExternalSystem { get; init; }
    public string? ExternalCode { get; init; }
}

public sealed class UpdateDiagnosisCodeRequest
{
    public required string Name { get; init; }
    public string? Category { get; init; }
    public string? ExternalSystem { get; init; }
    public string? ExternalCode { get; init; }
}

public sealed class SetActiveRequest
{
    public required bool IsActive { get; init; }
}

public sealed class CreateLabAnalyteRequest
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string ValueKind { get; init; }
    public required string DiagnosticKind { get; init; }
    public string? Unit { get; init; }
    public decimal? ReferenceLow { get; init; }
    public decimal? ReferenceHigh { get; init; }
    public string? NormalText { get; init; }
}

public sealed class UpdateLabAnalyteRequest
{
    public required string Name { get; init; }
    public required string ValueKind { get; init; }
    public required string DiagnosticKind { get; init; }
    public string? Unit { get; init; }
    public decimal? ReferenceLow { get; init; }
    public decimal? ReferenceHigh { get; init; }
    public string? NormalText { get; init; }
}

public sealed class CreateNoteTemplateRequest
{
    public required Guid ServiceId { get; init; }
    public required string Name { get; init; }
    public string Subjective { get; init; } = "";
    public string Objective { get; init; } = "";
    public string Assessment { get; init; } = "";
    public string Plan { get; init; } = "";
    public string DischargeInstructions { get; init; } = "";
}

public sealed class UpdateNoteTemplateRequest
{
    public required string Name { get; init; }
    public string Subjective { get; init; } = "";
    public string Objective { get; init; } = "";
    public string Assessment { get; init; } = "";
    public string Plan { get; init; } = "";
    public string DischargeInstructions { get; init; } = "";
}
