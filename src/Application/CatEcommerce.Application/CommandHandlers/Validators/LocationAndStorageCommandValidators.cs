using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-IMP-001 §11.2 -- shape only: ids non-empty, lengths and enumeration membership. The temperature BOUNDS and range (-40..60,
// min <= max -> InvalidTemperatureRange), uniqueness and the quarantine rule are the aggregate's and the handler's.

public sealed class CreateStockLocationCommandValidator : AbstractValidator<CreateStockLocationCommand>
{
    public CreateStockLocationCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(InventoryRules.MaxLocationCodeLength).MustBeCodeFormat().NoControlCharacters();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).NoControlCharacters();
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<CreateStockLocationCommand, StockLocationKind>();
    }
}

public sealed class UpdateStockLocationCommandValidator : AbstractValidator<UpdateStockLocationCommand>
{
    public UpdateStockLocationCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).NoControlCharacters();
    }
}

public sealed class SetStockLocationActivationCommandValidator : AbstractValidator<SetStockLocationActivationCommand>
{
    public SetStockLocationActivationCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class SetProductStorageProfileCommandValidator : AbstractValidator<SetProductStorageProfileCommand>
{
    public SetProductStorageProfileCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Condition).NotEmpty().MustBeValidEnumeration<SetProductStorageProfileCommand, StorageCondition>();
        RuleFor(x => x.HandlingNotes).MaximumLength(InventoryRules.NoteMaxLength).NoControlCharacters();
    }
}
