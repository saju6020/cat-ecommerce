using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-EMR-001 §11.2 -- shape only. "After today", "Booked, future, this cat's" are handler and aggregate rules.

public sealed class SetFollowUpCommandValidator : AbstractValidator<SetFollowUpCommand>
{
    public SetFollowUpCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<SetFollowUpCommand, FollowUpKind>();
        RuleFor(x => x.DueOn).NotEqual(default(DateOnly));
    }
}

public sealed class ClearFollowUpCommandValidator : AbstractValidator<ClearFollowUpCommand>
{
    public ClearFollowUpCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}
