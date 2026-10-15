using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-SRG-001 §11.2 -- shape only. That the surgery is ready, the dates sit in their windows and the instructions are present are aggregate rules.</summary>
public sealed class SetRecoveryStatusCommandValidator : AbstractValidator<SetRecoveryStatusCommand>
{
    public SetRecoveryStatusCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Status).MustBeValidEnumeration<SetRecoveryStatusCommand, RecoveryStatus>();
        RuleFor(x => x.Note).MaximumLength(SurgeryRules.NoteMax).NoControlCharacters().When(x => x.Note is not null);
    }
}

public sealed class DischargeSurgeryCommandValidator : AbstractValidator<DischargeSurgeryCommand>
{
    public DischargeSurgeryCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.PostOpInstructions).MaximumLength(SurgeryRules.PostOpInstructionsMax).NoControlCharacters().When(x => x.PostOpInstructions is not null);
        RuleFor(x => x.FollowUps).Must(f => f!.Count <= SurgeryRules.MaxFollowUps).When(x => x.FollowUps is not null).WithMessage($"A surgery has at most {SurgeryRules.MaxFollowUps} follow-ups.");
        RuleForEach(x => x.FollowUps).ChildRules(f => f.RuleFor(i => i.Kind).MustBeValidEnumeration<DischargeFollowUpInput, SurgeryFollowUpKind>()).When(x => x.FollowUps is not null);
    }
}

public sealed class CompleteSurgeryFollowUpCommandValidator : AbstractValidator<CompleteSurgeryFollowUpCommand>
{
    public CompleteSurgeryFollowUpCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.FollowUpId).NotEmpty();
    }
}

public sealed class CancelSurgeryFollowUpCommandValidator : AbstractValidator<CancelSurgeryFollowUpCommand>
{
    public CancelSurgeryFollowUpCommandValidator()
    {
        RuleFor(x => x.SurgeryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.FollowUpId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(SurgeryRules.CancellationReasonMax).NoControlCharacters();
    }
}
