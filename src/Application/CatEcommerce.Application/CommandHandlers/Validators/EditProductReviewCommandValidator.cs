using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class EditProductReviewCommandValidator : AbstractValidator<EditProductReviewCommand>
{
    public EditProductReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(ReviewRules.MinRating, ReviewRules.MaxRating);
        RuleFor(x => x.Title).MaximumLength(ReviewRules.TitleMaxLength).NoControlCharacters();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(ReviewRules.BodyMaxLength).NoControlCharacters();
    }
}

public sealed class WithdrawProductReviewCommandValidator : AbstractValidator<WithdrawProductReviewCommand>
{
    public WithdrawProductReviewCommandValidator() => RuleFor(x => x.ReviewId).NotEmpty();
}
