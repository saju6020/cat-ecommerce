using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>Shape rules shared by every command that carries an <c>Idempotency-Key</c> (ADR-187).</summary>
internal static class BillingKeyRules
{
    public static IRuleBuilderOptions<T, string?> IdempotencyKeyShape<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(BillingRules.MaxIdempotencyKeyLength).NoControlCharacters();

    public static IRuleBuilderOptions<T, IReadOnlyList<Guid>> ChargeIdList<T>(this IRuleBuilder<T, IReadOnlyList<Guid>> rule) =>
        rule.NotNull().Must(ids => ids is { Count: >= 1 } && ids.Count <= BillingRules.MaxChargeIdsPerRequest && ids.All(id => id != Guid.Empty))
            .WithMessage($"must name between 1 and {BillingRules.MaxChargeIdsPerRequest} charges.");
}

public sealed class CreateDraftInvoiceCommandValidator : AbstractValidator<CreateDraftInvoiceCommand>
{
    public CreateDraftInvoiceCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ChargeIds).ChargeIdList();
        RuleFor(x => x.Notes).MaximumLength(BillingRules.MaxNotesLength).NoControlCharacters();
    }
}

public sealed class SetDraftInvoiceChargesCommandValidator : AbstractValidator<SetDraftInvoiceChargesCommand>
{
    public SetDraftInvoiceChargesCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ChargeIds).ChargeIdList();
    }
}

public sealed class DiscardDraftInvoiceCommandValidator : AbstractValidator<DiscardDraftInvoiceCommand>
{
    public DiscardDraftInvoiceCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class IssueInvoiceCommandValidator : AbstractValidator<IssueInvoiceCommand>
{
    public IssueInvoiceCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IdempotencyKey).IdempotencyKeyShape();
    }
}

public sealed class IssueInvoiceFromChargesCommandValidator : AbstractValidator<IssueInvoiceFromChargesCommand>
{
    public IssueInvoiceFromChargesCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).IdempotencyKeyShape();

        RuleFor(x => x.Selector).NotNull();
        RuleFor(x => x.Selector).Must(s => s is not null && s.FormCount == 1)
            .WithMessage("Give exactly one of chargeIds, source or billingGroup.");

        When(x => x.Selector?.ChargeIds is not null, () => RuleFor(x => x.Selector.ChargeIds!).ChargeIdList());
        When(x => x.Selector?.Source is not null, () =>
        {
            RuleFor(x => x.Selector.Source!.Kind).NotEmpty().MustBeValidEnumeration<IssueInvoiceFromChargesCommand, ChargeSourceKind>();
            RuleFor(x => x.Selector.Source!.Id).NotEmpty();
        });
        When(x => x.Selector?.BillingGroup is not null, () =>
        {
            RuleFor(x => x.Selector.BillingGroup!.Kind).NotEmpty().MustBeValidEnumeration<IssueInvoiceFromChargesCommand, BillingGroupKind>();
            RuleFor(x => x.Selector.BillingGroup!.Id).NotEmpty();
        });
    }
}
