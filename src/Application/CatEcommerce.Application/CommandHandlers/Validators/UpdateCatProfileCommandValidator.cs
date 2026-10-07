using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class UpdateCatProfileCommandValidator : AbstractValidator<UpdateCatProfileCommand>
{
    public UpdateCatProfileCommandValidator()
    {
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Breed).MaximumLength(100);
        RuleFor(x => x.ColourAndMarkings).MaximumLength(200);

        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.DateOfBirth is not null)
            .WithMessage("Date of birth cannot be in the future.");

        RuleFor(x => x.DateOfBirth)
            .NotNull()
            .When(x => x.DateOfBirthPrecision == DateOfBirthPrecision.Exact)
            .WithMessage("Date of birth is required when precision is Exact.");
    }
}
