namespace CatEcommerce.Read.ViewModels;

// HO-EMR-001 §12.4 -- diagnostics. Every result value carries its flag and the range it was judged against;
// numbers are strings at the stored precision (UI-8).

public sealed record DiagnosticAnalyteViewModel(Guid AnalyteId, string Code, string Name);

public sealed record DiagnosticResultValueViewModel(
    Guid Id, string Name, string ValueKind, string? NumericValue, string? TextValue, string? Unit,
    string? ReferenceLow, string? ReferenceHigh, string? NormalText, string Flag);

public sealed record DiagnosticResultSetViewModel(
    Guid Id, DateOnly ReportedOn, string? Note, Guid? SupersedesResultSetId, string RecordedBy, DateTimeOffset RecordedOn,
    IReadOnlyList<DiagnosticResultValueViewModel> Values);

public sealed record DiagnosticAttachmentViewModel(
    Guid Id, DateOnly ResultDate, string? Note, string AttachedBy, DateTimeOffset AttachedOn, StoredFileViewModel? File);

/// <summary><c>GET /api/staff/diagnostics/{orderId}</c>.</summary>
public sealed record DiagnosticOrderViewModel(
    Guid Id, int Version, Guid VisitId, Guid CatId, string CatName, string Kind, string? Description, string Status,
    DateTimeOffset OrderedOn, string OrderedBy, string? CancelledReason, string BranchTimeZoneId,
    IReadOnlyList<DiagnosticAnalyteViewModel> RequestedAnalytes,
    IReadOnlyList<DiagnosticResultSetViewModel> ResultSets,
    IReadOnlyList<DiagnosticAttachmentViewModel> Attachments);

/// <summary><c>GET /api/staff/diagnostics?status=Pending</c> -- one row of the active branch's pending list.</summary>
public sealed record DiagnosticOrderListItemViewModel(
    Guid Id, Guid VisitId, Guid CatId, string CatName, string Kind, string Status, DateTimeOffset OrderedOn, string OrderedBy);
