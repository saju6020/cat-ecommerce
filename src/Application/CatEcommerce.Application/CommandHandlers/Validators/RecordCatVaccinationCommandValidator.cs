using CatEcommerce.Application.Commands.Cat;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RecordCatVaccinationCommandValidator : AbstractValidator<RecordCatVaccinationCommand>
{
    public RecordCatVaccinationCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.VaccinationId).NotEmpty();
        RuleFor(x => x.VaccineId).NotEmpty();
        RuleFor(x => x.AdministeredByVetId).NotEmpty();
        RuleFor(x => x.BatchReference).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);

        RuleFor(x => x.AdministeredOn)
            .LessThanOrEqualTo(_ => CatRecordDateBound.LatestPossibleLocalDate(timeProvider))
            .WithMessage("The vaccination date cannot be in the future.");
    }
}
