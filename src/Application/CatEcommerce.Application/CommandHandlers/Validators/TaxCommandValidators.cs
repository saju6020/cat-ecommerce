using CatEcommerce.Application.Commands.Administration;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class CreateTaxClassCommandValidator : AbstractValidator<CreateTaxClassCommand>
{
    public CreateTaxClassCommandValidator()
    {
        RuleFor(x => x.TaxClassId).NotEmpty();
        // Gate fix 2026-09-26 (SEC LOW): a format check, before the handler's own uniqueness
        // pre-check query runs; Name gets NoControlCharacters as free text.
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30).MustBeCodeFormat();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).NoControlCharacters();
    }
}

public sealed class UpdateTaxClassCommandValidator : AbstractValidator<UpdateTaxClassCommand>
{
    public UpdateTaxClassCommandValidator()
    {
        RuleFor(x => x.TaxClassId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).NoControlCharacters();
    }
}

public sealed class SetTaxRateCommandValidator : AbstractValidator<SetTaxRateCommand>
{
    public SetTaxRateCommandValidator()
    {
        RuleFor(x => x.TaxRateId).NotEmpty();
        RuleFor(x => x.TaxClassId).NotEmpty();
        RuleFor(x => x.RatePercent).InclusiveBetween(0, 100);
    }
}
