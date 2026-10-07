using CatEcommerce.Application.Commands.Product;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class ReactivateSkuCommandValidator : AbstractValidator<ReactivateSkuCommand>
{
    public ReactivateSkuCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.SkuId).NotEmpty();
    }
}
