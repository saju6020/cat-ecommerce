using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class SubmitProductReviewCommandValidator : AbstractValidator<SubmitProductReviewCommand>
{
    public SubmitProductReviewCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.SkuId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(ReviewRules.MinRating, ReviewRules.MaxRating);
        RuleFor(x => x.Title).MaximumLength(ReviewRules.TitleMaxLength).NoControlCharacters();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(ReviewRules.BodyMaxLength).NoControlCharacters();
    }
}
