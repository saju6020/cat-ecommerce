using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.DataMappers;

/// <summary>HO-EMR-001 §11.3 -- command to domain DTO for the three clinical catalogues. Enumeration
/// strings were already checked by each command's own <c>MustBeValidEnumeration</c> rule.</summary>
public static class ClinicalCatalogueMapper
{
    public static DiagnosisCodeCreateDto ToDomainDto(this CreateDiagnosisCodeCommand c) =>
        new(c.Code, c.Name, c.Category, c.ExternalSystem, c.ExternalCode, c.UserContext, c.CorrelationId);

    public static DiagnosisCodeUpdateDto ToDomainDto(this UpdateDiagnosisCodeCommand c) =>
        new(c.Name, c.Category, c.ExternalSystem, c.ExternalCode, c.UserContext, c.CorrelationId);

    public static CatalogueActiveDto ToDomainDto(this SetDiagnosisCodeActiveCommand c) => new(c.IsActive, c.UserContext, c.CorrelationId);

    public static LabAnalyteCreateDto ToDomainDto(this CreateLabAnalyteCommand c) =>
        new(c.Code, c.Name, AppointmentMapper.ParseEnum<ResultValueKind>(c.ValueKind), AppointmentMapper.ParseEnum<DiagnosticKind>(c.DiagnosticKind),
            c.Unit, c.ReferenceLow, c.ReferenceHigh, c.NormalText, c.UserContext, c.CorrelationId);

    public static LabAnalyteUpdateDto ToDomainDto(this UpdateLabAnalyteCommand c) =>
        new(c.Name, AppointmentMapper.ParseEnum<ResultValueKind>(c.ValueKind), AppointmentMapper.ParseEnum<DiagnosticKind>(c.DiagnosticKind),
            c.Unit, c.ReferenceLow, c.ReferenceHigh, c.NormalText, c.UserContext, c.CorrelationId);

    public static CatalogueActiveDto ToDomainDto(this SetLabAnalyteActiveCommand c) => new(c.IsActive, c.UserContext, c.CorrelationId);

    public static NoteTemplateCreateDto ToDomainDto(this CreateNoteTemplateCommand c) =>
        new(c.ClinicServiceId, c.Name, c.Subjective, c.Objective, c.Assessment, c.Plan, c.DischargeInstructions, c.UserContext, c.CorrelationId);

    public static NoteTemplateUpdateDto ToDomainDto(this UpdateNoteTemplateCommand c) =>
        new(c.Name, c.Subjective, c.Objective, c.Assessment, c.Plan, c.DischargeInstructions, c.UserContext, c.CorrelationId);

    public static CatalogueActiveDto ToDomainDto(this SetNoteTemplateActiveCommand c) => new(c.IsActive, c.UserContext, c.CorrelationId);
}
