using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-IMP-001 §11.2 -- shape only: ids, lengths, the HS-code regex, document content type and size, enumeration membership, exchange-rate bounds.
// Whether a country is real, a transition is legal, or clearance has its numbers are the aggregate's.

public sealed class HsCodeInputValidator : AbstractValidator<HsCodeInput>
{
    public HsCodeInputValidator()
    {
        RuleFor(h => h.Code).NotEmpty().Must(InventoryRules.IsValidHsCode).WithMessage("An HS code is four digits followed by up to three groups of two (for example 2309.10).");
        RuleFor(h => h.Description).MaximumLength(200).NoControlCharacters();
    }
}

public sealed class CostComponentInputValidator : AbstractValidator<CostComponentInput>
{
    public CostComponentInputValidator()
    {
        RuleFor(c => c.Kind).NotEmpty().MustBeValidEnumeration<CostComponentInput, CostComponentKind>();
        RuleFor(c => c.Amount).GreaterThanOrEqualTo(0m).ScalePrecision(2, 12, true);
        RuleFor(c => c.CurrencyCode).CurrencyCodeShape();
        RuleFor(c => c.Note).MaximumLength(200).NoControlCharacters();
    }
}

public sealed class ExchangeRateInputValidator : AbstractValidator<ExchangeRateInput>
{
    public ExchangeRateInputValidator()
    {
        RuleFor(r => r.CurrencyCode).CurrencyCodeShape();
        RuleFor(r => r.RateToBase).GreaterThan(0m).ScalePrecision(6, 18, true);
    }
}

internal static class ConsignmentRules
{
    public static void PurchaseOrderIds<T>(this AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, IReadOnlyList<Guid>>> selector) =>
        validator.RuleFor(selector).NotNull().Must(ids => ids is { Count: >= 1 and <= InventoryRules.MaxConsignmentPurchaseOrders } && ids.All(id => id != Guid.Empty))
            .WithMessage($"A consignment groups between 1 and {InventoryRules.MaxConsignmentPurchaseOrders} purchase orders.");
}

public sealed class PlanConsignmentCommandValidator : AbstractValidator<PlanConsignmentCommand>
{
    public PlanConsignmentCommandValidator()
    {
        RuleFor(x => x.ConsignmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.OriginCountryCode).CountryCodeShape();
        RuleFor(x => x.ShipmentReference).NotEmpty().MaximumLength(60).NoControlCharacters();
        this.PurchaseOrderIds(x => x.PurchaseOrderIds);
        RuleFor(x => x.HsCodes).NotNull().Must(h => h is { Count: <= InventoryRules.MaxHsCodes }).WithMessage($"At most {InventoryRules.MaxHsCodes} HS codes.");
        RuleForEach(x => x.HsCodes).SetValidator(new HsCodeInputValidator());
        RuleFor(x => x.ImportPermitNumber).MaximumLength(60).NoControlCharacters();
        RuleFor(x => x.HealthCertificateNumber).MaximumLength(60).NoControlCharacters();
        RuleFor(x => x.CustomsDeclarationNumber).MaximumLength(60).NoControlCharacters();
    }
}

public sealed class UpdateConsignmentDetailsCommandValidator : AbstractValidator<UpdateConsignmentDetailsCommand>
{
    public UpdateConsignmentDetailsCommandValidator()
    {
        RuleFor(x => x.ConsignmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.OriginCountryCode).CountryCodeShape();
        RuleFor(x => x.ShipmentReference).NotEmpty().MaximumLength(60).NoControlCharacters();
        this.PurchaseOrderIds(x => x.PurchaseOrderIds);
        RuleFor(x => x.HsCodes).NotNull().Must(h => h is { Count: <= InventoryRules.MaxHsCodes }).WithMessage($"At most {InventoryRules.MaxHsCodes} HS codes.");
        RuleForEach(x => x.HsCodes).SetValidator(new HsCodeInputValidator());
        RuleFor(x => x.ImportPermitNumber).MaximumLength(60).NoControlCharacters();
        RuleFor(x => x.HealthCertificateNumber).MaximumLength(60).NoControlCharacters();
        RuleFor(x => x.CustomsDeclarationNumber).MaximumLength(60).NoControlCharacters();
    }
}

public sealed class AdvanceConsignmentStatusCommandValidator : AbstractValidator<AdvanceConsignmentStatusCommand>
{
    public AdvanceConsignmentStatusCommandValidator()
    {
        RuleFor(x => x.ConsignmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.To).NotEmpty().MustBeValidEnumeration<AdvanceConsignmentStatusCommand, ConsignmentStatus>();
    }
}

public sealed class CancelConsignmentCommandValidator : AbstractValidator<CancelConsignmentCommand>
{
    public CancelConsignmentCommandValidator()
    {
        RuleFor(x => x.ConsignmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).NoControlCharacters();
    }
}

public sealed class AddConsignmentDocumentCommandValidator : AbstractValidator<AddConsignmentDocumentCommand>
{
    public AddConsignmentDocumentCommandValidator()
    {
        RuleFor(x => x.ConsignmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<AddConsignmentDocumentCommand, ImportDocumentKind>();
        RuleFor(x => x.DocumentNumber).MaximumLength(60).NoControlCharacters();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255).NoControlCharacters();
        RuleFor(x => x.StorageKey).NotEmpty().MaximumLength(500).NoControlCharacters();
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SizeInBytes).GreaterThan(0);
    }
}

public sealed class SupersedeConsignmentDocumentCommandValidator : AbstractValidator<SupersedeConsignmentDocumentCommand>
{
    public SupersedeConsignmentDocumentCommandValidator()
    {
        RuleFor(x => x.ConsignmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.SupersededDocumentId).NotEmpty();
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<SupersedeConsignmentDocumentCommand, ImportDocumentKind>();
        RuleFor(x => x.DocumentNumber).MaximumLength(60).NoControlCharacters();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255).NoControlCharacters();
        RuleFor(x => x.StorageKey).NotEmpty().MaximumLength(500).NoControlCharacters();
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SizeInBytes).GreaterThan(0);
    }
}

public sealed class ReplaceConsignmentCostsCommandValidator : AbstractValidator<ReplaceConsignmentCostsCommand>
{
    public ReplaceConsignmentCostsCommandValidator()
    {
        RuleFor(x => x.ConsignmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CostComponents).NotNull().Must(c => c is { Count: <= InventoryRules.MaxCostComponents }).WithMessage($"At most {InventoryRules.MaxCostComponents} cost components.");
        RuleForEach(x => x.CostComponents).SetValidator(new CostComponentInputValidator());
        RuleFor(x => x.ExchangeRates).NotNull().Must(r => r is { Count: <= InventoryRules.MaxExchangeRates }).WithMessage($"At most {InventoryRules.MaxExchangeRates} exchange rates.");
        RuleForEach(x => x.ExchangeRates).SetValidator(new ExchangeRateInputValidator());
    }
}

public sealed class AllocateLandedCostCommandValidator : AbstractValidator<AllocateLandedCostCommand>
{
    public AllocateLandedCostCommandValidator()
    {
        RuleFor(x => x.ConsignmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}
