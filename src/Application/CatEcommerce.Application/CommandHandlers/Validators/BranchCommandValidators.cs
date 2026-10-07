using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-ADM-001 §11.2 -- shape only. Time-zone validity and interval invariants are the
/// aggregate's job.</summary>
public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        // Gate fix 2026-09-26 (SEC LOW): a format check, before the handler's own uniqueness
        // pre-check query runs.
        RuleFor(x => x.Code).NotEmpty().MaximumLength(BranchRules.CodeMaxLength).MustBeCodeFormat();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(BranchRules.NameMaxLength).NoControlCharacters();
        RuleFor(x => x.TimeZoneId).NotEmpty();
        RuleFor(x => x.AddressLine).MaximumLength(BranchRules.AddressMaxLength).NoControlCharacters();
        // Gate fix 2026-09-26 (SEC LOW, hardening round): a NUL byte previously reached Postgres
        // unfiltered.
        RuleFor(x => x.PhoneNumber).MaximumLength(BranchRules.PhoneMaxLength).NoControlCharacters();
    }
}

public sealed class UpdateBranchDetailsCommandValidator : AbstractValidator<UpdateBranchDetailsCommand>
{
    public UpdateBranchDetailsCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(BranchRules.NameMaxLength).NoControlCharacters();
        RuleFor(x => x.TimeZoneId).NotEmpty();
        RuleFor(x => x.AddressLine).MaximumLength(BranchRules.AddressMaxLength).NoControlCharacters();
        RuleFor(x => x.PhoneNumber).MaximumLength(BranchRules.PhoneMaxLength).NoControlCharacters();
    }
}

public sealed class SetBranchWeeklyHoursCommandValidator : AbstractValidator<SetBranchWeeklyHoursCommand>
{
    public SetBranchWeeklyHoursCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Intervals).NotNull();
    }
}

public sealed class SetBranchActivationCommandValidator : AbstractValidator<SetBranchActivationCommand>
{
    public SetBranchActivationCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
    }
}
