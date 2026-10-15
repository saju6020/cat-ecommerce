using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-PHM-001 §11.2 -- shape only. Whether a repeat can be requested, the validity window and the vet's role are aggregate or handler rules.
// Registered as IValidator<T> (rule 21).

public sealed class RequestRepeatCommandValidator : AbstractValidator<RequestRepeatCommand>
{
    public RequestRepeatCommandValidator()
    {
        RuleFor(x => x.PrescriptionId).NotEmpty();
        RuleFor(x => x.RepeatRequestId).NotEmpty();
        RuleFor(x => x.PreferredBranchId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(RepeatRules.MaxNoteLength).NoControlCharacters();
    }
}

public sealed class WithdrawRepeatRequestCommandValidator : AbstractValidator<WithdrawRepeatRequestCommand>
{
    public WithdrawRepeatRequestCommandValidator()
    {
        RuleFor(x => x.PrescriptionId).NotEmpty();
        RuleFor(x => x.RepeatRequestId).NotEmpty();
    }
}

public sealed class DeclineRepeatRequestCommandValidator : AbstractValidator<DeclineRepeatRequestCommand>
{
    public DeclineRepeatRequestCommandValidator()
    {
        RuleFor(x => x.PrescriptionId).NotEmpty();
        RuleFor(x => x.RepeatRequestId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(RepeatRules.MaxDeclineReasonLength).NoControlCharacters();
    }
}

public sealed class RenewPrescriptionCommandValidator : AbstractValidator<RenewPrescriptionCommand>
{
    public RenewPrescriptionCommandValidator()
    {
        PrescribingValidationRules.ApplyFieldRules(this);
        RuleFor(x => x.NewPrescriptionId).NotEmpty();
        RuleFor(x => x.FromPrescriptionId).NotEmpty();
        RuleFor(x => x.VisitId).NotEqual(Guid.Empty).When(x => x.VisitId is not null);
    }
}

public sealed class ExpirePrescriptionCommandValidator : AbstractValidator<ExpirePrescriptionCommand>
{
    public ExpirePrescriptionCommandValidator() => RuleFor(x => x.PrescriptionId).NotEmpty();
}
