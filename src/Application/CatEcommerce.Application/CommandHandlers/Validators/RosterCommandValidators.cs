using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class SetRosterPatternCommandValidator : AbstractValidator<SetRosterPatternCommand>
{
    public SetRosterPatternCommandValidator()
    {
        RuleFor(x => x.StaffAccountId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Slots).NotNull();
    }
}

public sealed class CancelScheduledRosterPatternCommandValidator : AbstractValidator<CancelScheduledRosterPatternCommand>
{
    public CancelScheduledRosterPatternCommandValidator()
    {
        RuleFor(x => x.StaffAccountId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class CreateRosterExceptionCommandValidator : AbstractValidator<CreateRosterExceptionCommand>
{
    public CreateRosterExceptionCommandValidator()
    {
        RuleFor(x => x.RosterExceptionId).NotEmpty();
        RuleFor(x => x.StaffAccountId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        // Gate fix 2026-09-26 (SEC LOW, hardening round): unknown/wrongly-cased -> 400, not a 500.
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<CreateRosterExceptionCommand, RosterExceptionKind>();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200).NoControlCharacters();
    }
}

public sealed class CancelRosterExceptionCommandValidator : AbstractValidator<CancelRosterExceptionCommand>
{
    public CancelRosterExceptionCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.RosterExceptionId).NotEmpty();
    }
}
