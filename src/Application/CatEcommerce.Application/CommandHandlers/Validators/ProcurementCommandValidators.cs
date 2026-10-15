using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-IMP-001 §11.2 -- shape only: ids non-empty, lengths, country/currency code SHAPE (two/three upper letters), quantities > 0 with at most
// three decimals, line counts within InventoryRules. Whether the country is a real ISO-3166 code, SKU flags, supplier state and every
// transition are the aggregate's and the handler's.

internal static class ProcurementRules
{
    public static IRuleBuilderOptions<T, string> CountryCodeShape<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Length(2).Matches("^[A-Z]{2}$").WithMessage("must be a two-letter upper-case country code.");

    public static IRuleBuilderOptions<T, string> CurrencyCodeShape<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Length(3).Matches("^[A-Z]{3}$").WithMessage("must be a three-letter upper-case currency code.");
}

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).MustBeCodeFormat().NoControlCharacters();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.CountryCode).CountryCodeShape();
        RuleFor(x => x.CurrencyCode).CurrencyCodeShape();
        RuleFor(x => x.ContactName).MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.Phone).MaximumLength(30).NoControlCharacters();
        RuleFor(x => x.Email).MaximumLength(256).NoControlCharacters();
        RuleFor(x => x.Address).MaximumLength(500).NoControlCharacters();
        RuleFor(x => x.PaymentTerms).NotEmpty().MaximumLength(500).NoControlCharacters();
        RuleFor(x => x.Notes).MaximumLength(1000).NoControlCharacters();
    }
}

public sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.CountryCode).CountryCodeShape();
        RuleFor(x => x.CurrencyCode).CurrencyCodeShape();
        RuleFor(x => x.ContactName).MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.Phone).MaximumLength(30).NoControlCharacters();
        RuleFor(x => x.Email).MaximumLength(256).NoControlCharacters();
        RuleFor(x => x.Address).MaximumLength(500).NoControlCharacters();
        RuleFor(x => x.PaymentTerms).NotEmpty().MaximumLength(500).NoControlCharacters();
        RuleFor(x => x.Notes).MaximumLength(1000).NoControlCharacters();
    }
}

public sealed class SetSupplierActivationCommandValidator : AbstractValidator<SetSupplierActivationCommand>
{
    public SetSupplierActivationCommandValidator() => RuleFor(x => x.SupplierId).NotEmpty();
}

public sealed class PurchaseOrderLineInputValidator : AbstractValidator<PurchaseOrderLineInput>
{
    public PurchaseOrderLineInputValidator()
    {
        RuleFor(l => l.LineId).NotEmpty();
        RuleFor(l => l.SkuId).NotEmpty();
        RuleFor(l => l.OrderedQuantity).GreaterThan(0m).ScalePrecision(3, 12, true);
        RuleFor(l => l.UnitCostAmount).GreaterThanOrEqualTo(0m).ScalePrecision(4, 14, true);
    }
}

public sealed class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.DeliverToLocationId).NotEmpty();
        RuleFor(x => x.CurrencyCode!).CurrencyCodeShape().When(x => x.CurrencyCode is not null);
        RuleFor(x => x.Notes).MaximumLength(1000).NoControlCharacters();
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: >= 1 and <= InventoryRules.MaxPurchaseOrderLines })
            .WithMessage($"A purchase order has between 1 and {InventoryRules.MaxPurchaseOrderLines} lines.");
        RuleForEach(x => x.Lines).SetValidator(new PurchaseOrderLineInputValidator());
    }
}

public sealed class ReplacePurchaseOrderDraftCommandValidator : AbstractValidator<ReplacePurchaseOrderDraftCommand>
{
    public ReplacePurchaseOrderDraftCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.DeliverToLocationId).NotEmpty();
        RuleFor(x => x.CurrencyCode!).CurrencyCodeShape().When(x => x.CurrencyCode is not null);
        RuleFor(x => x.Notes).MaximumLength(1000).NoControlCharacters();
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: >= 1 and <= InventoryRules.MaxPurchaseOrderLines })
            .WithMessage($"A purchase order has between 1 and {InventoryRules.MaxPurchaseOrderLines} lines.");
        RuleForEach(x => x.Lines).SetValidator(new PurchaseOrderLineInputValidator());
    }
}

public sealed class IssuePurchaseOrderCommandValidator : AbstractValidator<IssuePurchaseOrderCommand>
{
    public IssuePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class CancelPurchaseOrderCommandValidator : AbstractValidator<CancelPurchaseOrderCommand>
{
    public CancelPurchaseOrderCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).NoControlCharacters();
    }
}

public sealed class ClosePurchaseOrderCommandValidator : AbstractValidator<ClosePurchaseOrderCommand>
{
    public ClosePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).NoControlCharacters();
    }
}
