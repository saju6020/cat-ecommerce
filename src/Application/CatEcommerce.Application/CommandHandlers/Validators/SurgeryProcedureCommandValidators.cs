using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-SRG-001 §11.2 -- shape only. That every required report field is PRESENT is a finalisation rule (it names the missing fields); a draft may be incomplete.</summary>
public sealed class EndSurgicalProcedureCommandValidator : AbstractValidator<EndSurgicalProcedureCommand>
{
    public EndSurgicalProcedureCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Outcome).MustBeValidEnumeration<EndSurgicalProcedureCommand, ProcedureOutcome>();
    }
}

public sealed class SaveSurgicalReportDraftCommandValidator : AbstractValidator<SaveSurgicalReportDraftCommand>
{
    public SaveSurgicalReportDraftCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).When(x => x.ExpectedVersion is not null);
        RuleFor(x => x.ProcedurePerformed).MaximumLength(SurgeryRules.ReportTextMax).NoControlCharacters();
        RuleFor(x => x.Findings).MaximumLength(SurgeryRules.ReportTextMax).NoControlCharacters();
        RuleFor(x => x.Technique).MaximumLength(SurgeryRules.ReportTextMax).NoControlCharacters();
        RuleFor(x => x.SutureMaterial).MaximumLength(500).NoControlCharacters();
        RuleFor(x => x.Complications).MaximumLength(SurgeryRules.ReportTextMax).NoControlCharacters();
        RuleFor(x => x.SurgeonId).NotEmpty();
        RuleFor(x => x.AssistantId).NotEmpty();
    }
}

public sealed class FinaliseSurgicalReportCommandValidator : AbstractValidator<FinaliseSurgicalReportCommand>
{
    public FinaliseSurgicalReportCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public sealed class AddSurgicalReportAddendumCommandValidator : AbstractValidator<AddSurgicalReportAddendumCommand>
{
    public AddSurgicalReportAddendumCommandValidator()
    {
        RuleFor(x => x.AddendumId).NotEmpty();
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(SurgeryRules.AddendumMax).NoControlCharacters();
    }
}

public sealed class ApplySterilisationOutcomeCommandValidator : AbstractValidator<ApplySterilisationOutcomeCommand>
{
    public ApplySterilisationOutcomeCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}
