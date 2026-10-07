using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class PublishProductReviewCommandValidator : AbstractValidator<PublishProductReviewCommand>
{
    public PublishProductReviewCommandValidator() => RuleFor(x => x.ReviewId).NotEmpty();
}

public sealed class RejectProductReviewCommandValidator : AbstractValidator<RejectProductReviewCommand>
{
    public RejectProductReviewCommandValidator()
    {
        // Deliberately no NotEmpty() on Reason: a blank reason is section 8.11's own
        // ReviewRejectionReasonRequired rule, raised by the aggregate, not a shape failure here
        // (see RejectProductReviewCommandHandler's own comment).
        RuleFor(x => x.ReviewId).NotEmpty();
        RuleFor(x => x.Reason).MustBeValidEnumeration<RejectProductReviewCommand, ReviewRejectionReason>();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class HideProductReviewCommandValidator : AbstractValidator<HideProductReviewCommand>
{
    public HideProductReviewCommandValidator() => RuleFor(x => x.ReviewId).NotEmpty();
}
