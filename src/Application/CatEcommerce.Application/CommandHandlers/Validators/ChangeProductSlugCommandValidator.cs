using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class ChangeProductSlugCommandValidator : AbstractValidator<ChangeProductSlugCommand>
{
    public ChangeProductSlugCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(ProductRules.SlugMaxLength).MustBeSlugFormat();
    }
}
