using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-XCT-001 section 11.2 -- shape only. Hold dates and ledger state are aggregate rules.

public sealed class OpenErasureRequestCommandValidator : AbstractValidator<OpenErasureRequestCommand>
{
    public OpenErasureRequestCommandValidator()
    {
        RuleFor(x => x.CustomerAccountId).NotEmpty();
        RuleFor(x => x.Source).MustBeValidEnumeration<OpenErasureRequestCommand, ErasureRequestSource>();
    }
}

public sealed class ExecuteErasureScopeCommandValidator : AbstractValidator<ExecuteErasureScopeCommand>
{
    public ExecuteErasureScopeCommandValidator()
    {
        RuleFor(x => x.CustomerAccountId).NotEmpty();
        RuleFor(x => x.ContextKey).NotEmpty().MaximumLength(ErasureScope.MaxContextKeyLength);
    }
}

public sealed class EraseCustomerOnRequestCommandValidator : AbstractValidator<EraseCustomerOnRequestCommand>
{
    public const int MaxFreeTextLength = 500;

    public EraseCustomerOnRequestCommandValidator()
    {
        RuleFor(x => x.CustomerAccountId).NotEmpty();
        RuleFor(x => x.Channel).MustBeValidEnumeration<EraseCustomerOnRequestCommand, ErasureRequestChannel>();
        RuleFor(x => x.IdentityCheck).NotEmpty().MustBeValidEnumeration<EraseCustomerOnRequestCommand, ErasureIdentityCheck>();
    }
}

public sealed class ReapplyErasuresAfterRestoreCommandValidator : AbstractValidator<ReapplyErasuresAfterRestoreCommand>
{
    public const int MaxRestoreReferenceLength = 200;

    public ReapplyErasuresAfterRestoreCommandValidator()
    {
        // One generic, stable code for the list size so that an operator script can branch on it.
        RuleFor(x => x.CustomerAccountIds).NotEmpty();
        RuleFor(x => x.CustomerAccountIds)
            .Must(ids => ids.Count <= RetentionRules.ReapplyMaxIds)
            .WithErrorCode(BusinessViolationCodes.ReapplyListTooLarge)
            .WithMessage(OperationsMessages.ReapplyListTooLarge);
        RuleFor(x => x.CustomerAccountIds)
            .Must(ids => ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count)
            .WithMessage("The ids must be distinct and non-empty.");
        RuleFor(x => x.RestoreReference).NotEmpty().MaximumLength(MaxRestoreReferenceLength).NoControlCharacters();
    }
}
