using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Visit;

// HO-EMR-001 §11.1 C1-C3 -- the three clinical catalogues. Data only; ids minted in the controller.

public sealed class CreateDiagnosisCodeCommand : Command
{
    public required Guid DiagnosisCodeId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Category { get; init; }
    public string? ExternalSystem { get; init; }
    public string? ExternalCode { get; init; }
}

public sealed class UpdateDiagnosisCodeCommand : Command
{
    public required Guid DiagnosisCodeId { get; init; }
    public required string Name { get; init; }
    public string? Category { get; init; }
    public string? ExternalSystem { get; init; }
    public string? ExternalCode { get; init; }
}

public sealed class SetDiagnosisCodeActiveCommand : Command
{
    public required Guid DiagnosisCodeId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class CreateLabAnalyteCommand : Command
{
    public required Guid LabAnalyteId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string ValueKind { get; init; }
    public required string DiagnosticKind { get; init; }
    public string? Unit { get; init; }
    public decimal? ReferenceLow { get; init; }
    public decimal? ReferenceHigh { get; init; }
    public string? NormalText { get; init; }
}

public sealed class UpdateLabAnalyteCommand : Command
{
    public required Guid LabAnalyteId { get; init; }
    public required string Name { get; init; }
    public required string ValueKind { get; init; }
    public required string DiagnosticKind { get; init; }
    public string? Unit { get; init; }
    public decimal? ReferenceLow { get; init; }
    public decimal? ReferenceHigh { get; init; }
    public string? NormalText { get; init; }
}

public sealed class SetLabAnalyteActiveCommand : Command
{
    public required Guid LabAnalyteId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class CreateNoteTemplateCommand : Command
{
    public required Guid NoteTemplateId { get; init; }
    public required Guid ClinicServiceId { get; init; }
    public required string Name { get; init; }
    public string Subjective { get; init; } = "";
    public string Objective { get; init; } = "";
    public string Assessment { get; init; } = "";
    public string Plan { get; init; } = "";
    public string DischargeInstructions { get; init; } = "";
}

public sealed class UpdateNoteTemplateCommand : Command
{
    public required Guid NoteTemplateId { get; init; }
    public required string Name { get; init; }
    public string Subjective { get; init; } = "";
    public string Objective { get; init; } = "";
    public string Assessment { get; init; } = "";
    public string Plan { get; init; } = "";
    public string DischargeInstructions { get; init; } = "";
}

public sealed class SetNoteTemplateActiveCommand : Command
{
    public required Guid NoteTemplateId { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>C4 -- dispatched by <c>ClinicalRecordAccessFilter</c> before every clinical read
/// (§8.11, ADR-129). <see cref="Kind"/> is the <see cref="ClinicalRecordKind"/> display name.</summary>
public sealed class RecordClinicalRecordAccessCommand : Command
{
    public required Guid AuditEntryId { get; init; }
    public Guid? CatId { get; init; }
    public required ClinicalRecordKind Kind { get; init; }
    public Guid? RecordId { get; init; }
    public required string Path { get; init; }
    public string? IpAddress { get; init; }
}
