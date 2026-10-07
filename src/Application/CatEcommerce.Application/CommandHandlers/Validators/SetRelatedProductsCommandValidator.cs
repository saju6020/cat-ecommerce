using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class SetRelatedProductsCommandValidator : AbstractValidator<SetRelatedProductsCommand>
{
    public SetRelatedProductsCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Links).NotNull();
        RuleForEach(x => x.Links).ChildRules(link =>
        {
            link.RuleFor(l => l.RelatedProductId).NotEmpty();
            link.RuleFor(l => l.Kind).Must(k => EnumerationParsing.TryParse<RelatedProductKind>(k, out _))
                .WithMessage("Kind must be FrequentlyBoughtTogether or UpSell.");
            link.RuleFor(l => l.SortOrder).InclusiveBetween(0, 999);
        });
    }
}
