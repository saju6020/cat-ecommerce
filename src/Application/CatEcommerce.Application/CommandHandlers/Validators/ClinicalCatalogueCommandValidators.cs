using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-EMR-001 §11.2 -- shape only: ids non-empty, lengths, enum membership, numbers finite. Range
// ordering, uniqueness and the system-code rule are aggregate or handler rules.

public sealed class CreateDiagnosisCodeCommandValidator : AbstractValidator<CreateDiagnosisCodeCommand>
{
    public CreateDiagnosisCodeCommandValidator()
    {
        RuleFor(x => x.DiagnosisCodeId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(ClinicalNoteRules.MaxDiagnosisCodeLength).NoControlCharacters();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ClinicalNoteRules.MaxDiagnosisNameLength).NoControlCharacters();
        RuleFor(x => x.Category).MaximumLength(ClinicalNoteRules.MaxCategoryLength).NoControlCharacters();
        RuleFor(x => x.ExternalSystem).MaximumLength(ClinicalNoteRules.MaxExternalCodeLength).NoControlCharacters();
        RuleFor(x => x.ExternalCode).MaximumLength(ClinicalNoteRules.MaxExternalCodeLength).NoControlCharacters();
    }
}

public sealed class UpdateDiagnosisCodeCommandValidator : AbstractValidator<UpdateDiagnosisCodeCommand>
{
    public UpdateDiagnosisCodeCommandValidator()
    {
        RuleFor(x => x.DiagnosisCodeId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ClinicalNoteRules.MaxDiagnosisNameLength).NoControlCharacters();
        RuleFor(x => x.Category).MaximumLength(ClinicalNoteRules.MaxCategoryLength).NoControlCharacters();
        RuleFor(x => x.ExternalSystem).MaximumLength(ClinicalNoteRules.MaxExternalCodeLength).NoControlCharacters();
        RuleFor(x => x.ExternalCode).MaximumLength(ClinicalNoteRules.MaxExternalCodeLength).NoControlCharacters();
    }
}

public sealed class SetDiagnosisCodeActiveCommandValidator : AbstractValidator<SetDiagnosisCodeActiveCommand>
{
    public SetDiagnosisCodeActiveCommandValidator() => RuleFor(x => x.DiagnosisCodeId).NotEmpty();
}

public sealed class CreateLabAnalyteCommandValidator : AbstractValidator<CreateLabAnalyteCommand>
{
    public CreateLabAnalyteCommandValidator()
    {
        RuleFor(x => x.LabAnalyteId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(DiagnosticRules.MaxAnalyteCodeLength).NoControlCharacters();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ClinicalNoteRules.MaxCatalogueNameLength).NoControlCharacters();
        RuleFor(x => x.ValueKind).NotEmpty().MustBeValidEnumeration<CreateLabAnalyteCommand, ResultValueKind>();
        RuleFor(x => x.DiagnosticKind).NotEmpty().MustBeValidEnumeration<CreateLabAnalyteCommand, DiagnosticKind>();
        RuleFor(x => x.Unit).MaximumLength(DiagnosticRules.MaxUnitLength).NoControlCharacters();
        RuleFor(x => x.NormalText).MaximumLength(DiagnosticRules.MaxNormalTextLength).NoControlCharacters();
    }
}

public sealed class UpdateLabAnalyteCommandValidator : AbstractValidator<UpdateLabAnalyteCommand>
{
    public UpdateLabAnalyteCommandValidator()
    {
        RuleFor(x => x.LabAnalyteId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ClinicalNoteRules.MaxCatalogueNameLength).NoControlCharacters();
        RuleFor(x => x.ValueKind).NotEmpty().MustBeValidEnumeration<UpdateLabAnalyteCommand, ResultValueKind>();
        RuleFor(x => x.DiagnosticKind).NotEmpty().MustBeValidEnumeration<UpdateLabAnalyteCommand, DiagnosticKind>();
        RuleFor(x => x.Unit).MaximumLength(DiagnosticRules.MaxUnitLength).NoControlCharacters();
        RuleFor(x => x.NormalText).MaximumLength(DiagnosticRules.MaxNormalTextLength).NoControlCharacters();
    }
}

public sealed class SetLabAnalyteActiveCommandValidator : AbstractValidator<SetLabAnalyteActiveCommand>
{
    public SetLabAnalyteActiveCommandValidator() => RuleFor(x => x.LabAnalyteId).NotEmpty();
}

public sealed class CreateNoteTemplateCommandValidator : AbstractValidator<CreateNoteTemplateCommand>
{
    public CreateNoteTemplateCommandValidator()
    {
        RuleFor(x => x.NoteTemplateId).NotEmpty();
        RuleFor(x => x.ClinicServiceId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ClinicalNoteRules.MaxCatalogueNameLength).NoControlCharacters();
        RuleFor(x => x.Subjective).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.Objective).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.Assessment).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.Plan).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.DischargeInstructions).NotNull().MaximumLength(ClinicalNoteRules.MaxDischargeInstructionsLength).NoControlCharacters();
    }
}

public sealed class UpdateNoteTemplateCommandValidator : AbstractValidator<UpdateNoteTemplateCommand>
{
    public UpdateNoteTemplateCommandValidator()
    {
        RuleFor(x => x.NoteTemplateId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ClinicalNoteRules.MaxCatalogueNameLength).NoControlCharacters();
        RuleFor(x => x.Subjective).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.Objective).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.Assessment).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.Plan).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.DischargeInstructions).NotNull().MaximumLength(ClinicalNoteRules.MaxDischargeInstructionsLength).NoControlCharacters();
    }
}

public sealed class SetNoteTemplateActiveCommandValidator : AbstractValidator<SetNoteTemplateActiveCommand>
{
    public SetNoteTemplateActiveCommandValidator() => RuleFor(x => x.NoteTemplateId).NotEmpty();
}

public sealed class RecordClinicalRecordAccessCommandValidator : AbstractValidator<RecordClinicalRecordAccessCommand>
{
    public RecordClinicalRecordAccessCommandValidator()
    {
        RuleFor(x => x.AuditEntryId).NotEmpty();
        RuleFor(x => x.Kind).NotNull();
        RuleFor(x => x.Path).NotEmpty().MaximumLength(500);
    }
}
