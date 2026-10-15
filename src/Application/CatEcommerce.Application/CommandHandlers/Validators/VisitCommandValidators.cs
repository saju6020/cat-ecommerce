using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-EMR-001 §11.2 -- shape only. Ranges (absolute and plausible), "at least one value", and every
// finalisation rule are aggregate rules, not validator rules.

public sealed class OpenVisitCommandValidator : AbstractValidator<OpenVisitCommand>
{
    public OpenVisitCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public sealed class RecordVitalsCommandValidator : AbstractValidator<RecordVitalsCommand>
{
    public RecordVitalsCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.VitalsEntryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();

        // The precision of each measurement is the stated one (§8.3): 2 dp weight, 1 dp temperature.
        RuleFor(x => x.WeightKg).Must(v => v is null || decimal.Round(v.Value, 2) == v.Value)
            .WithMessage("Weight is stated to at most 2 decimal places.");
        RuleFor(x => x.TemperatureC).Must(v => v is null || decimal.Round(v.Value, 1) == v.Value)
            .WithMessage("Temperature is stated to at most 1 decimal place.");

        RuleFor(x => x.ConfirmedOutOfRange).NotNull();
        RuleForEach(x => x.ConfirmedOutOfRange).Must(name => EnumerationParsing.TryParse<VitalSign>(name, out _))
            .WithMessage("'{PropertyValue}' is not a recognised VitalSign.");
        RuleFor(x => x.Note).MaximumLength(ClinicalNoteRules.MaxVitalsNoteLength).NoControlCharacters();
    }
}
