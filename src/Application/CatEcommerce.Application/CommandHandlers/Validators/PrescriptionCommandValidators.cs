using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-PHM-001 §11.2 -- shape only. Dose ranges, weight recency, visit state, validity windows and the acknowledgement rules are aggregate, domain-service or
// handler rules. Registered as IValidator<T> (rule 21).

internal static class PrescribingValidationRules
{
    public static void ApplyFieldRules<T>(AbstractValidator<T> validator) where T : PrescribingFieldsCommandBase
    {
        validator.RuleFor(x => x.BranchId).NotEmpty();
        validator.RuleFor(x => x.Dose).NotNull().ChildRules(d =>
        {
            d.RuleFor(y => y.Amount).GreaterThan(0m).MaxDecimalPlaces(4);
            d.RuleFor(y => y.Unit).NotEmpty().MustBeValidEnumeration<DoseModel, DoseUnit>();
        });
        validator.RuleFor(x => x.Route).NotEmpty().MustBeValidEnumeration<T, AdministrationRoute>();
        validator.RuleFor(x => x.Frequency).NotEmpty().MustBeValidEnumeration<T, DoseFrequency>();
        validator.RuleFor(x => x.DurationDays).InclusiveBetween(PrescriptionRules.MinDurationDays, PrescriptionRules.MaxDurationDays);
        validator.RuleFor(x => x.Quantity).GreaterThan(0m).MaxDecimalPlaces(2).LessThan(100000m);
        validator.RuleFor(x => x.Instructions).NotEmpty().MaximumLength(PrescriptionRules.MaxInstructionsLength).NoControlCharacters();
        validator.RuleFor(x => x.RepeatsAuthorised).InclusiveBetween(0, PrescriptionRules.MaxRepeats);
        validator.RuleFor(x => x.RepeatsAuthorised).GreaterThan(0).When(x => x.IsRepeatable).WithMessage("A repeatable prescription authorises at least one repeat.");
        validator.RuleFor(x => x.RepeatsAuthorised).Equal(0).When(x => !x.IsRepeatable).WithMessage("A prescription that is not repeatable authorises no repeats.");
        validator.RuleFor(x => x.ValidUntil).NotNull().When(x => x.IsRepeatable).WithMessage("A repeatable prescription needs the date its repeats stop.");
        validator.RuleFor(x => x.AcknowledgedWarningsFingerprint).MaximumLength(64).NoControlCharacters();
        validator.RuleFor(x => x.AllergyAcknowledgementReason).MaximumLength(PrescriptionRules.MaxNoteLength).NoControlCharacters();
        validator.RuleFor(x => x.DoseAcknowledgementNote).MaximumLength(PrescriptionRules.MaxNoteLength).NoControlCharacters();
    }
}

public sealed class IssuePrescriptionCommandValidator : AbstractValidator<IssuePrescriptionCommand>
{
    public IssuePrescriptionCommandValidator()
    {
        PrescribingValidationRules.ApplyFieldRules(this);
        RuleFor(x => x.PrescriptionId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.MedicineId).NotEmpty();
    }
}

public sealed class CancelPrescriptionCommandValidator : AbstractValidator<CancelPrescriptionCommand>
{
    public CancelPrescriptionCommandValidator()
    {
        RuleFor(x => x.PrescriptionId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(PrescriptionRules.MaxReasonLength).NoControlCharacters();
    }
}
