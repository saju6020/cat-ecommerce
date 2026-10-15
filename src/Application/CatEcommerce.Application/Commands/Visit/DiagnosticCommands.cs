using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Visit;

// HO-EMR-001 §11.1 C12-C15 (Phase D). Files are registered by HO-FIL-001's upload endpoint; a command
// carries a FileId only -- never bytes, never an IFormFile (§4.4).

/// <summary>C12 -- orders a diagnostic on an <b>open</b> visit; the order is pending until results arrive (US-CLN-14 AC1).</summary>
public sealed class OrderDiagnosticCommand : Command
{
    public required Guid DiagnosticOrderId { get; init; }
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Kind { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<Guid> AnalyteIds { get; init; } = [];
}

/// <summary>One value of a result set as entered. Name, unit and reference range fall back to the analyte's when the
/// lab report did not state its own (the report's range wins when entered -- FR-CLN-16).</summary>
public sealed class ResultValueInput
{
    public required Guid ValueId { get; init; }
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

/// <summary>C13 -- appends a result set. Allowed after the visit is finalised (the report arrives later). A correction is
/// a new set naming the one it supersedes; nothing is overwritten (ADR-124).</summary>
public sealed class RecordDiagnosticResultsCommand : Command
{
    public required Guid DiagnosticOrderId { get; init; }
    public required Guid ResultSetId { get; init; }
    public required Guid BranchId { get; init; }
    public required DateOnly ReportedOn { get; init; }
    public required IReadOnlyList<ResultValueInput> Values { get; init; }
    public string? Note { get; init; }
    public Guid? SupersedesResultSetId { get; init; }
}

/// <summary>One recorded value with the flag it was judged to carry and the range it was judged against.</summary>
public sealed record RecordedResultValue(
    Guid ValueId, string Name, string ValueKind, decimal? NumericValue, string? TextValue, string? Unit,
    decimal? ReferenceLow, decimal? ReferenceHigh, string? NormalText, string Flag);

public sealed record RecordResultsResult(Guid ResultSetId, IReadOnlyList<RecordedResultValue> Values);

/// <summary>C14 -- attaches a stored lab file (PDF, JPEG, PNG, or DICOM as a file) with its date and a note.</summary>
public sealed class AttachDiagnosticFileCommand : Command
{
    public required Guid DiagnosticOrderId { get; init; }
    public required Guid AttachmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid FileId { get; init; }
    public required DateOnly ResultDate { get; init; }
    public string? Note { get; init; }
}

/// <summary>C14 -- cancels a pending order that has no results and no files.</summary>
public sealed class CancelDiagnosticOrderCommand : Command
{
    public required Guid DiagnosticOrderId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}

/// <summary>C15 -- a dated, noted clinical photograph on an open visit (US-CLN-19).</summary>
public sealed class AttachClinicalPhotoCommand : Command
{
    public required Guid VisitId { get; init; }
    public required Guid PhotoId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid FileId { get; init; }
    public required DateOnly TakenOn { get; init; }
    public string? Note { get; init; }
}
