using CatEcommerce.Application.Commands.Vaccination;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RegisterVaccineCommandValidator : AbstractValidator<RegisterVaccineCommand>
{
    public RegisterVaccineCommandValidator()
    {
        RuleFor(x => x.VaccineId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.ValidityMonths).InclusiveBetween(1, 120);
    }
}
