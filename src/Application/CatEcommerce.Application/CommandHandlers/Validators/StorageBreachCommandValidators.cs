using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-IMP-001 section 11.2 -- shape only. An empty condition, a nonsense temperature and a note outside 5-500 characters are the aggregate's refusals.

public sealed class ReportStorageBreachCommandValidator : AbstractValidator<ReportStorageBreachCommand>
{
    /// <summary>A clock that is a little fast must not refuse an honest report made a moment ago.</summary>
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(5);

    public ReportStorageBreachCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.ObservedOn).Must(on => on <= timeProvider.GetUtcNow() + Skew).WithMessage("The time a breach was observed cannot be in the future.");
        RuleFor(x => x.BreachId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Condition).MaximumLength(InventoryRules.NoteMaxLength).NoControlCharacters();
    }
}

public sealed class QuarantineStorageBreachStockCommandValidator : AbstractValidator<QuarantineStorageBreachStockCommand>
{
    public QuarantineStorageBreachStockCommandValidator()
    {
        RuleFor(x => x.BreachId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class CloseStorageBreachCommandValidator : AbstractValidator<CloseStorageBreachCommand>
{
    public CloseStorageBreachCommandValidator()
    {
        RuleFor(x => x.BreachId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(InventoryRules.NoteMaxLength).NoControlCharacters();
    }
}
