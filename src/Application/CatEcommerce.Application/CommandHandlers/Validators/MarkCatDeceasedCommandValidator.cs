using CatEcommerce.Application.Commands.Cat;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class MarkCatDeceasedCommandValidator : AbstractValidator<MarkCatDeceasedCommand>
{
    public MarkCatDeceasedCommandValidator()
    {
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500);

        RuleFor(x => x.DeceasedOn)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("The deceased date cannot be in the future.");
    }
}
