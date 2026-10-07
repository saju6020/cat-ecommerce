using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>Shape only (section 11.2): ids, quantity range, collection size. Duplicates, bundle /
/// prescription / discontinued components and the unit minimum are the aggregate's rules.</summary>
public sealed class SetBundleComponentsCommandValidator : AbstractValidator<SetBundleComponentsCommand>
{
    public SetBundleComponentsCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Components).NotNull();
        RuleForEach(x => x.Components).ChildRules(component =>
        {
            component.RuleFor(c => c.ComponentSkuId).NotEmpty();
            component.RuleFor(c => c.Quantity).InclusiveBetween(1, BundleRules.MaxComponentQuantity);
        });
    }
}
