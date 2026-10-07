using CatEcommerce.Application.Commands.Cat;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RecordCatVaccinationCommandValidator : AbstractValidator<RecordCatVaccinationCommand>
{
    public RecordCatVaccinationCommandValidator()
    {
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.VaccinationId).NotEmpty();
        RuleFor(x => x.VaccineId).NotEmpty();
        RuleFor(x => x.AdministeredByVetId).NotEmpty();
        RuleFor(x => x.BatchReference).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);

        RuleFor(x => x.AdministeredOn)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("A vaccination cannot be recorded with a future date.");
    }
}
