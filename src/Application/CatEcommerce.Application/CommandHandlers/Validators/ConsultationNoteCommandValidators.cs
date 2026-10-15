using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-EMR-001 §11.2 -- shape only. Clinical text is plain text: control characters other than tab and line
// breaks are refused (they could never reach Postgres' text type anyway), and nothing is ever treated as markup.

public sealed class SaveConsultationDraftCommandValidator : AbstractValidator<SaveConsultationDraftCommand>
{
    public SaveConsultationDraftCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.DraftId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Subjective).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.Objective).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.Assessment).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.Plan).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.DischargeInstructions).NotNull().MaximumLength(ClinicalNoteRules.MaxDischargeInstructionsLength).NoControlCharacters();
        RuleFor(x => x.ProblemCodeIds).NotNull().Must(ids => ids.Count <= ClinicalNoteRules.MaxProblems)
            .WithMessage($"A draft lists at most {ClinicalNoteRules.MaxProblems} problems.");
    }
}

public sealed class ProblemInputValidator : AbstractValidator<ProblemInput>
{
    public ProblemInputValidator()
    {
        RuleFor(x => x.ProblemId).NotEmpty();
        RuleFor(x => x.DiagnosisCodeId).NotEmpty();
        RuleFor(x => x.Certainty).NotEmpty().MustBeValidEnumeration<ProblemInput, ProblemCertainty>();
        RuleFor(x => x.Note).MaximumLength(ClinicalNoteRules.MaxProblemNoteLength).NoControlCharacters();
    }
}

public sealed class SaveConsultationNoteCommandValidator : AbstractValidator<SaveConsultationNoteCommand>
{
    public SaveConsultationNoteCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Subjective).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.Objective).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.Assessment).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.Plan).NotNull().MaximumLength(ClinicalNoteRules.MaxSoapFieldLength).NoControlCharacters();
        RuleFor(x => x.DischargeInstructions).NotNull().MaximumLength(ClinicalNoteRules.MaxDischargeInstructionsLength).NoControlCharacters();

        RuleFor(x => x.Problems).NotNull().Must(p => p.Count <= ClinicalNoteRules.MaxProblems)
            .WithMessage($"A visit holds at most {ClinicalNoteRules.MaxProblems} problems.");
        RuleForEach(x => x.Problems).SetValidator(new ProblemInputValidator());

        RuleFor(x => x.AppliedTemplateVersion).GreaterThan(0).When(x => x.AppliedTemplateVersion is not null);
        RuleFor(x => x.AppliedTemplateId).NotNull().When(x => x.AppliedTemplateVersion is not null)
            .WithMessage("A template version needs the template it belongs to.");
    }
}

public sealed class FinaliseVisitCommandValidator : AbstractValidator<FinaliseVisitCommand>
{
    public FinaliseVisitCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public sealed class AddVisitAddendumCommandValidator : AbstractValidator<AddVisitAddendumCommand>
{
    public AddVisitAddendumCommandValidator()
    {
        RuleFor(x => x.AddendumId).NotEmpty();
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(ClinicalNoteRules.MaxAddendumLength).NoControlCharacters();
        RuleFor(x => x.Reason).MaximumLength(ClinicalNoteRules.MaxAddendumReasonLength).NoControlCharacters();
    }
}
