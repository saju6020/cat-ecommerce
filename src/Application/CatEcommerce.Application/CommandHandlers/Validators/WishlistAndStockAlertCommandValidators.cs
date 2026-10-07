using CatEcommerce.Application.Commands.Product;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class AddWishlistItemCommandValidator : AbstractValidator<AddWishlistItemCommand>
{
    public AddWishlistItemCommandValidator() => RuleFor(x => x.SkuId).NotEmpty();
}

public sealed class RemoveWishlistItemCommandValidator : AbstractValidator<RemoveWishlistItemCommand>
{
    public RemoveWishlistItemCommandValidator() => RuleFor(x => x.SkuId).NotEmpty();
}

public sealed class RequestStockAlertCommandValidator : AbstractValidator<RequestStockAlertCommand>
{
    public RequestStockAlertCommandValidator()
    {
        RuleFor(x => x.StockAlertId).NotEmpty();
        RuleFor(x => x.SkuId).NotEmpty();
    }
}

public sealed class CancelStockAlertCommandValidator : AbstractValidator<CancelStockAlertCommand>
{
    public CancelStockAlertCommandValidator() => RuleFor(x => x.StockAlertId).NotEmpty();
}

public sealed class NotifyStockAlertCommandValidator : AbstractValidator<NotifyStockAlertCommand>
{
    public NotifyStockAlertCommandValidator() => RuleFor(x => x.StockAlertId).NotEmpty();
}
