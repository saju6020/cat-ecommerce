using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Domain.ValueObjects;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record RequestedAnalyteDto(Guid AnalyteId, string Code, string Name);

/// <summary><see cref="VisitStatus"/> is the visit's status (a diagnostic is ordered on an open visit
/// only, §8.5).</summary>
public sealed record DiagnosticOrderDto(
    Guid VisitId,
    Guid CatId,
    Guid BranchId,
    VisitStatus VisitStatus,
    DiagnosticKind Kind,
    string? Description,
    IReadOnlyList<RequestedAnalyteDto> Analytes,
    Guid OrderedBy,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId,
    Money? UnitPrice = null,
    Guid? TaxClassId = null);

/// <summary>One value of a result set as entered. Name, unit and range already carry the analyte's
/// defaults where the lab report did not enter its own (the handler applies them -- §8.5: "the lab
/// report's range wins when entered").</summary>
public sealed record ResultValueDto(
    Guid ValueId,
    Guid? AnalyteId,
    string Name,
    ResultValueKind ValueKind,
    decimal? NumericValue,
    string? TextValue,
    string? Unit,
    decimal? ReferenceLow,
    decimal? ReferenceHigh,
    string? NormalText);

public sealed record DiagnosticResultSetDto(
    Guid ResultSetId,
    DateOnly ReportedOn,
    string? Note,
    Guid? SupersedesResultSetId,
    IReadOnlyList<ResultValueDto> Values,
    Guid RecordedBy,
    DateOnly Today,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId);

public sealed record DiagnosticAttachmentDto(
    Guid AttachmentId,
    Guid FileId,
    DateOnly ResultDate,
    string? Note,
    Guid AttachedBy,
    DateOnly Today,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId);

public sealed record DiagnosticCancelDto(string Reason, Guid CancelledBy, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);
