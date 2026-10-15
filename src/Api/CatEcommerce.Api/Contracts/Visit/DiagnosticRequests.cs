namespace CatEcommerce.Api.Contracts.Visit;

/// <summary>HO-EMR-001 §12.4 -- diagnostics request bodies. A file is referenced by the id the upload endpoint returned.</summary>
public sealed class OrderDiagnosticRequest
{
    public required string Kind { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<Guid> AnalyteIds { get; init; } = [];
}

public sealed class ResultValueRequest
{
    public Guid? ValueId { get; init; }
    public Guid? AnalyteId { get; init; }
    public string? Name { get; init; }
    public string? ValueKind { get; init; }
    public decimal? NumericValue { get; init; }
    public string? TextValue { get; init; }
    public string? Unit { get; init; }
    public decimal? ReferenceLow { get; init; }
    public decimal? ReferenceHigh { get; init; }
    public string? NormalText { get; init; }
}

public sealed class RecordDiagnosticResultsRequest
{
    public required DateOnly ReportedOn { get; init; }
    public required IReadOnlyList<ResultValueRequest> Values { get; init; }
    public string? Note { get; init; }
    public Guid? SupersedesResultSetId { get; init; }
}

public sealed class AttachDiagnosticFileRequest
{
    public required Guid FileId { get; init; }
    public required DateOnly ResultDate { get; init; }
    public string? Note { get; init; }
}

public sealed class CancelDiagnosticOrderRequest
{
    public required string Reason { get; init; }
}

public sealed class AttachClinicalPhotoRequest
{
    public required Guid FileId { get; init; }
    public required DateOnly TakenOn { get; init; }
    public string? Note { get; init; }
}
