using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-IMP-001 §11.2 -- shape only. The adjustment's reason and note are MANDATORY but their absence is the AGGREGATE's refusal
// (AdjustmentReasonRequired / AdjustmentNoteRequired -- ADR-107), so the validator checks only lengths and enumeration membership: the 400 names the
// business code the screen keys on.

public sealed class StockTransferLineInputValidator : AbstractValidator<StockTransferLineInput>
{
    public StockTransferLineInputValidator()
    {
        RuleFor(l => l.LineId).NotEmpty();
        RuleFor(l => l.SkuId).NotEmpty();
        RuleFor(l => l.BatchId).NotEmpty();
        RuleFor(l => l.Quantity).GreaterThan(0m).ScalePrecision(3, 12, true);
    }
}

public sealed class PostStockTransferCommandValidator : AbstractValidator<PostStockTransferCommand>
{
    public PostStockTransferCommandValidator()
    {
        RuleFor(x => x.TransferId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.FromLocationId).NotEmpty();
        RuleFor(x => x.ToLocationId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(InventoryRules.NoteMaxLength).NoControlCharacters();
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: >= 1 and <= InventoryRules.MaxTransferLines })
            .WithMessage($"A transfer has between 1 and {InventoryRules.MaxTransferLines} lines.");
        RuleForEach(x => x.Lines).SetValidator(new StockTransferLineInputValidator());
    }
}

public sealed class AdjustmentLineInputValidator : AbstractValidator<AdjustmentLineInput>
{
    public AdjustmentLineInputValidator()
    {
        RuleFor(l => l.LineId).NotEmpty();
        RuleFor(l => l.SkuId).NotEmpty();
        RuleFor(l => l.BatchId).NotEmpty();
        RuleFor(l => l.QuantityDelta).ScalePrecision(3, 12, true);
    }
}

public sealed class RequestStockAdjustmentCommandValidator : AbstractValidator<RequestStockAdjustmentCommand>
{
    public RequestStockAdjustmentCommandValidator()
    {
        RuleFor(x => x.AdjustmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Reason!).MustBeValidEnumeration<RequestStockAdjustmentCommand, AdjustmentReason>().When(x => !string.IsNullOrWhiteSpace(x.Reason));
        RuleFor(x => x.Note).MaximumLength(InventoryRules.NoteMaxLength).NoControlCharacters();
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: >= 1 and <= InventoryRules.MaxAdjustmentLines })
            .WithMessage($"An adjustment has between 1 and {InventoryRules.MaxAdjustmentLines} lines.");
        RuleForEach(x => x.Lines).SetValidator(new AdjustmentLineInputValidator());
    }
}

public sealed class ApproveStockAdjustmentCommandValidator : AbstractValidator<ApproveStockAdjustmentCommand>
{
    public ApproveStockAdjustmentCommandValidator()
    {
        RuleFor(x => x.AdjustmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(InventoryRules.NoteMaxLength).NoControlCharacters();
    }
}

public sealed class RejectStockAdjustmentCommandValidator : AbstractValidator<RejectStockAdjustmentCommand>
{
    public RejectStockAdjustmentCommandValidator()
    {
        RuleFor(x => x.AdjustmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Note).NotEmpty().MaximumLength(InventoryRules.NoteMaxLength).NoControlCharacters();
    }
}

public sealed class WithdrawStockAdjustmentCommandValidator : AbstractValidator<WithdrawStockAdjustmentCommand>
{
    public WithdrawStockAdjustmentCommandValidator()
    {
        RuleFor(x => x.AdjustmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class OpenStockCountCommandValidator : AbstractValidator<OpenStockCountCommand>
{
    public OpenStockCountCommandValidator()
    {
        RuleFor(x => x.CountId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Scope).NotEmpty().MustBeValidEnumeration<OpenStockCountCommand, StockCountScope>();
        RuleFor(x => x.SkuIds).NotNull().Must(ids => ids is { Count: <= InventoryRules.MaxCountSkus } && ids.All(id => id != Guid.Empty))
            .WithMessage($"At most {InventoryRules.MaxCountSkus} SKUs.");
    }
}

public sealed class CountLineInputValidator : AbstractValidator<CountLineInput>
{
    public CountLineInputValidator()
    {
        RuleFor(l => l.SkuId).NotEmpty();
        RuleFor(l => l.BatchId).NotEmpty();
        RuleFor(l => l.CountedQuantity).GreaterThanOrEqualTo(0m).ScalePrecision(3, 12, true);
    }
}

public sealed class RecordCountLinesCommandValidator : AbstractValidator<RecordCountLinesCommand>
{
    public RecordCountLinesCommandValidator()
    {
        RuleFor(x => x.CountId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: >= 1 and <= InventoryRules.MaxCountLines })
            .WithMessage($"A request records between 1 and {InventoryRules.MaxCountLines} count lines.");
        RuleForEach(x => x.Lines).SetValidator(new CountLineInputValidator());
    }
}

public sealed class SubmitStockCountCommandValidator : AbstractValidator<SubmitStockCountCommand>
{
    public SubmitStockCountCommandValidator() { RuleFor(x => x.CountId).NotEmpty(); RuleFor(x => x.BranchId).NotEmpty(); }
}

public sealed class CommitStockCountCommandValidator : AbstractValidator<CommitStockCountCommand>
{
    public CommitStockCountCommandValidator() { RuleFor(x => x.CountId).NotEmpty(); RuleFor(x => x.BranchId).NotEmpty(); }
}

public sealed class ApproveStockCountCommandValidator : AbstractValidator<ApproveStockCountCommand>
{
    public ApproveStockCountCommandValidator() { RuleFor(x => x.CountId).NotEmpty(); RuleFor(x => x.BranchId).NotEmpty(); }
}

public sealed class RejectStockCountCommandValidator : AbstractValidator<RejectStockCountCommand>
{
    public RejectStockCountCommandValidator() { RuleFor(x => x.CountId).NotEmpty(); RuleFor(x => x.BranchId).NotEmpty(); }
}

public sealed class CancelStockCountCommandValidator : AbstractValidator<CancelStockCountCommand>
{
    public CancelStockCountCommandValidator() { RuleFor(x => x.CountId).NotEmpty(); RuleFor(x => x.BranchId).NotEmpty(); }
}
