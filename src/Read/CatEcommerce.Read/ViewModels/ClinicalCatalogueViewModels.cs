namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-EMR-001 §12.4 -- clinical catalogue reads (diagnosis codes, lab analytes, note templates).</summary>
public sealed record DiagnosisCodeViewModel(
    Guid Id, string Code, string Name, string? Category, string? ExternalSystem, string? ExternalCode, bool IsActive, bool IsSystem, int Version);

public sealed record LabAnalyteViewModel(
    Guid Id, string Code, string Name, string ValueKind, string DiagnosticKind, string? Unit,
    string? ReferenceLow, string? ReferenceHigh, string? NormalText, bool IsActive, int Version);

public sealed record NoteTemplateViewModel(
    Guid Id, Guid ServiceId, string ServiceName, string Name, string Subjective, string Objective, string Assessment, string Plan,
    string DischargeInstructions, int TemplateVersion, bool IsActive, int Version);
