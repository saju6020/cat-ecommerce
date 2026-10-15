using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-IMP-001 §11.2 -- shape only. A negative reorder point or a zero reorder quantity is the aggregate's refusal (InvalidReorderPolicy).

public sealed class SetReorderPolicyCommandValidator : AbstractValidator<SetReorderPolicyCommand>
{
    public SetReorderPolicyCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.PreferredSupplierId).NotEqual(Guid.Empty).When(x => x.PreferredSupplierId is not null);
    }
}

public sealed class RaiseStockAlertCommandValidator : AbstractValidator<RaiseStockAlertCommand>
{
    public RaiseStockAlertCommandValidator()
    {
        RuleFor(x => x.AlertId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<RaiseStockAlertCommand, StockAlertKind>();
        RuleFor(x => x.DedupKey).MaximumLength(200);
        RuleFor(x => x.ThresholdDays).InclusiveBetween(0, 730).When(x => x.ThresholdDays is not null);
    }
}

public sealed class ResolveStockAlertCommandValidator : AbstractValidator<ResolveStockAlertCommand>
{
    public ResolveStockAlertCommandValidator()
    {
        RuleFor(x => x.AlertId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(InventoryRules.NoteMaxLength).NoControlCharacters();
    }
}

public sealed class AcknowledgeStockAlertCommandValidator : AbstractValidator<AcknowledgeStockAlertCommand>
{
    public AcknowledgeStockAlertCommandValidator()
    {
        RuleFor(x => x.AlertId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(InventoryRules.NoteMaxLength).NoControlCharacters();
    }
}
