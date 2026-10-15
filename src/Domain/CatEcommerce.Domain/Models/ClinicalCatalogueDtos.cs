using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record DiagnosisCodeCreateDto(
    string Code, string Name, string? Category, string? ExternalSystem, string? ExternalCode,
    UserContext UserContext, Guid CorrelationId);

public sealed record DiagnosisCodeUpdateDto(
    string Name, string? Category, string? ExternalSystem, string? ExternalCode,
    UserContext UserContext, Guid CorrelationId);

public sealed record CatalogueActiveDto(bool IsActive, UserContext UserContext, Guid CorrelationId);

public sealed record LabAnalyteCreateDto(
    string Code, string Name, ResultValueKind ValueKind, DiagnosticKind DiagnosticKind, string? Unit,
    decimal? ReferenceLow, decimal? ReferenceHigh, string? NormalText,
    UserContext UserContext, Guid CorrelationId);

public sealed record LabAnalyteUpdateDto(
    string Name, ResultValueKind ValueKind, DiagnosticKind DiagnosticKind, string? Unit,
    decimal? ReferenceLow, decimal? ReferenceHigh, string? NormalText,
    UserContext UserContext, Guid CorrelationId);

public sealed record NoteTemplateCreateDto(
    Guid ClinicServiceId, string Name, string Subjective, string Objective, string Assessment, string Plan,
    string DischargeInstructions, UserContext UserContext, Guid CorrelationId);

public sealed record NoteTemplateUpdateDto(
    string Name, string Subjective, string Objective, string Assessment, string Plan,
    string DischargeInstructions, UserContext UserContext, Guid CorrelationId);
