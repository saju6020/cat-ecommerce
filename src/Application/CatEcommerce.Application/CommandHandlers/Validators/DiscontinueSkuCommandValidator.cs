using CatEcommerce.Application.Commands.Product;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class DiscontinueSkuCommandValidator : AbstractValidator<DiscontinueSkuCommand>
{
    public DiscontinueSkuCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.SkuId).NotEmpty();
    }
}
