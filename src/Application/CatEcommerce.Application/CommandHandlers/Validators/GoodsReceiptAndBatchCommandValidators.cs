using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-IMP-001 §11.2 -- shape only. EXPIRY (D-2, BR-3), the dates, PO outstanding, cost and rate, location and duplicates are the aggregate's:
// the handler collects every one of them with GoodsReceiptAggregate.Validate so the screen shows all violations at once.

public sealed class GoodsReceiptLineInputValidator : AbstractValidator<GoodsReceiptLineInput>
{
    public GoodsReceiptLineInputValidator()
    {
        RuleFor(l => l.LineId).NotEmpty();
        RuleFor(l => l.SkuId).NotEmpty();
        RuleFor(l => l.LocationId).NotEmpty();
        RuleFor(l => l.BatchId).NotEmpty();
        RuleFor(l => l.LotNumber).MaximumLength(InventoryRules.MaxLotNumberLength).NoControlCharacters();
        RuleFor(l => l.QuantityAccepted).ScalePrecision(3, 12, true);
        RuleFor(l => l.QuantityRejected).ScalePrecision(3, 12, true);
        RuleFor(l => l.RejectionReason!).MustBeValidEnumeration<GoodsReceiptLineInput, RejectionReason>().When(l => !string.IsNullOrWhiteSpace(l.RejectionReason));
        RuleFor(l => l.RejectionNote).MaximumLength(InventoryRules.NoteMaxLength).NoControlCharacters();
        RuleFor(l => l.UnitCostAmount).ScalePrecision(4, 14, true).When(l => l.UnitCostAmount is not null);
        RuleFor(l => l.UnitCostCurrency!).CurrencyCodeShape().When(l => l.UnitCostCurrency is not null);
        RuleFor(l => l.ExchangeRateToBase).ScalePrecision(6, 18, true).When(l => l.ExchangeRateToBase is not null);
    }
}

public sealed class PostGoodsReceiptCommandValidator : AbstractValidator<PostGoodsReceiptCommand>
{
    public PostGoodsReceiptCommandValidator()
    {
        RuleFor(x => x.GoodsReceiptId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.DeliveryReference).MaximumLength(60).NoControlCharacters();
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: >= 1 and <= InventoryRules.MaxReceiptLines })
            .WithMessage($"A goods receipt has between 1 and {InventoryRules.MaxReceiptLines} lines.");
        RuleForEach(x => x.Lines).SetValidator(new GoodsReceiptLineInputValidator());
    }
}

public sealed class QuarantineBatchCommandValidator : AbstractValidator<QuarantineBatchCommand>
{
    public QuarantineBatchCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000).NoControlCharacters();
    }
}

public sealed class ReleaseBatchCommandValidator : AbstractValidator<ReleaseBatchCommand>
{
    public ReleaseBatchCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class WriteOffBatchCommandValidator : AbstractValidator<WriteOffBatchCommand>
{
    public WriteOffBatchCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000).NoControlCharacters();
    }
}
