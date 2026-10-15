using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-IMP-001 section 11.2 -- shape only. An empty reason or advice, and a lot number that does not match, are the aggregate's refusals (RecallConfirmationMismatch).

public sealed class RecallBatchCommandValidator : AbstractValidator<RecallBatchCommand>
{
    public RecallBatchCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.ConfirmLotNumber).MaximumLength(InventoryRules.MaxLotNumberLength).NoControlCharacters();
        RuleFor(x => x.Reason).MaximumLength(RecallRules.ReasonMaxLength).NoControlCharacters();
        RuleFor(x => x.CustomerAdvice).MaximumLength(RecallRules.AdviceMaxLength).NoControlCharacters();
    }
}

public sealed class NotifyRecallRecipientsCommandValidator : AbstractValidator<NotifyRecallRecipientsCommand>
{
    public NotifyRecallRecipientsCommandValidator() => RuleFor(x => x.BatchId).NotEmpty();
}

public sealed class WithdrawBatchRecallCommandValidator : AbstractValidator<WithdrawBatchRecallCommand>
{
    public WithdrawBatchRecallCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(RecallRules.ReasonMaxLength).NoControlCharacters();
    }
}

public sealed class CloseBatchRecallCommandValidator : AbstractValidator<CloseBatchRecallCommand>
{
    public CloseBatchRecallCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(RecallRules.ReasonMaxLength).NoControlCharacters();
    }
}

public sealed class RecordRecallTraceExportCommandValidator : AbstractValidator<RecordRecallTraceExportCommand>
{
    public RecordRecallTraceExportCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.RowCount).GreaterThanOrEqualTo(0);
    }
}
