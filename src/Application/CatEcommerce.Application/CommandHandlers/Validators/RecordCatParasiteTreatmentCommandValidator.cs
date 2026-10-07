using CatEcommerce.Application.Commands.Cat;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RecordCatParasiteTreatmentCommandValidator : AbstractValidator<RecordCatParasiteTreatmentCommand>
{
    public RecordCatParasiteTreatmentCommandValidator()
    {
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.TreatmentId).NotEmpty();
        RuleFor(x => x.AdministeredByStaffId).NotEmpty();
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BatchReference).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.IntervalDays).InclusiveBetween(1, 730);

        RuleFor(x => x.AdministeredOn)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("The treatment date cannot be in the future.");
    }
}
