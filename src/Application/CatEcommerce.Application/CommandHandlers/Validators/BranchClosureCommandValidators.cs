using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class CreateBranchClosureCommandValidator : AbstractValidator<CreateBranchClosureCommand>
{
    public CreateBranchClosureCommandValidator()
    {
        RuleFor(x => x.ClosureId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.EndsOn).GreaterThanOrEqualTo(x => x.StartsOn);
        // Gate fix 2026-09-26 (SEC LOW, hardening round): unknown/wrongly-cased -> 400, not a 500
        // out of AdministrationMapper's Enumeration.FromDisplayName.
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<CreateBranchClosureCommand, ClosureKind>();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200).NoControlCharacters();
    }
}

public sealed class CancelBranchClosureCommandValidator : AbstractValidator<CancelBranchClosureCommand>
{
    public CancelBranchClosureCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ClosureId).NotEmpty();
    }
}
