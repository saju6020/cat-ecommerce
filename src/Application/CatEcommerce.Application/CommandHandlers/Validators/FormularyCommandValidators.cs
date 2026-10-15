using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-PHM-001 §11.2 -- shape only. Dose ranges, the route/basis relationship, controlled-schedule and SKU rules are aggregate or handler rules.
// Registered as IValidator<T>; the platform resolves the interface (rule 21).

public static class PharmacyValidationExtensions
{
    /// <summary>At most <paramref name="places"/> decimal places (the column's scale) -- a value with more is refused before it is rounded silently.</summary>
    public static IRuleBuilderOptions<T, decimal> MaxDecimalPlaces<T>(this IRuleBuilder<T, decimal> ruleBuilder, int places) =>
        ruleBuilder.Must(value => decimal.Round(value, places) == value).WithMessage($"must have at most {places} decimal places.");

    public static IRuleBuilderOptions<T, decimal?> MaxDecimalPlaces<T>(this IRuleBuilder<T, decimal?> ruleBuilder, int places) =>
        ruleBuilder.Must(value => value is null || decimal.Round(value.Value, places) == value.Value).WithMessage($"must have at most {places} decimal places.");
}

internal static class FormularyValidationRules
{
    public static void ApplyBaseRules<T>(AbstractValidator<T> validator) where T : FormularyMedicineCommandBase
    {
        validator.RuleFor(x => x.MedicineId).NotEmpty();
        validator.RuleFor(x => x.Name).NotEmpty().MaximumLength(FormularyRules.MaxNameLength).NoControlCharacters();
        validator.RuleFor(x => x.ActiveIngredients).NotNull().Must(a => a is { Count: >= 1 and <= FormularyRules.MaxActiveIngredients }).WithMessage("A medicine has one to five active ingredients.");
        validator.RuleForEach(x => x.ActiveIngredients).NotEmpty().MaximumLength(FormularyRules.MaxTermLength).NoControlCharacters();
        validator.RuleFor(x => x.DrugClasses).NotNull().Must(c => c.Count <= FormularyRules.MaxDrugClasses).WithMessage("A medicine has at most ten drug classes.");
        validator.RuleForEach(x => x.DrugClasses).NotEmpty().MaximumLength(FormularyRules.MaxTermLength).NoControlCharacters();
        validator.RuleFor(x => x.Form).NotEmpty().MustBeValidEnumeration<T, MedicineForm>();
        validator.RuleFor(x => x.Strength).NotNull().ChildRules(s =>
        {
            s.RuleFor(y => y.Unit).NotEmpty().MustBeValidEnumeration<StrengthModel, StrengthUnit>();
            s.RuleFor(y => y.PerUnit).NotEmpty().MustBeValidEnumeration<StrengthModel, DispensingUnit>();
            s.RuleFor(y => y.Amount).GreaterThan(0m).MaxDecimalPlaces(4);
            s.RuleFor(y => y.PerQuantity).GreaterThan(0m).MaxDecimalPlaces(4);
        });
        validator.RuleFor(x => x.DefaultRoute).NotEmpty().MustBeValidEnumeration<T, AdministrationRoute>();
        validator.RuleFor(x => x.AllowedRoutes).NotNull().Must(r => r.Count >= 1).WithMessage("At least one route is allowed.");
        validator.RuleForEach(x => x.AllowedRoutes).NotEmpty().MustBeValidEnumeration<T, AdministrationRoute>();
        validator.RuleFor(x => x.DosingBasis).NotEmpty().MustBeValidEnumeration<T, DosingBasis>();
        validator.RuleFor(x => x.DoseRange!).ChildRules(r =>
        {
            r.RuleFor(y => y.MinMgPerKg).GreaterThan(0m).MaxDecimalPlaces(4);
            r.RuleFor(y => y.MaxMgPerKg).GreaterThan(0m).MaxDecimalPlaces(4);
        }).When(x => x.DoseRange is not null);
        validator.RuleFor(x => x.Contraindications).MaximumLength(FormularyRules.MaxContraindicationsLength).NoControlCharacters();
        validator.RuleFor(x => x.Interactions).NotNull().Must(i => i.Count <= FormularyRules.MaxInteractions).WithMessage("A medicine lists at most 50 interactions.");
        validator.RuleForEach(x => x.Interactions).ChildRules(i =>
        {
            i.RuleFor(y => y.Severity).NotEmpty().MustBeValidEnumeration<InteractionModel, InteractionSeverity>();
            i.RuleFor(y => y.Note).NotEmpty().MaximumLength(FormularyRules.MaxInteractionNoteLength).NoControlCharacters();
            i.RuleFor(y => y.InteractsWithTerm).MaximumLength(FormularyRules.MaxTermLength).NoControlCharacters();
        });
        validator.RuleFor(x => x.DispensingUnit).NotEmpty().MustBeValidEnumeration<T, DispensingUnit>();
        validator.RuleFor(x => x.LinkedSkuIds).NotNull().Must(s => s.Count <= FormularyRules.MaxLinkedSkus).WithMessage("A medicine links at most ten SKUs.");
        validator.RuleForEach(x => x.LinkedSkuIds).NotEmpty();
    }
}

public sealed class CreateFormularyMedicineCommandValidator : AbstractValidator<CreateFormularyMedicineCommand>
{
    public CreateFormularyMedicineCommandValidator()
    {
        FormularyValidationRules.ApplyBaseRules(this);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(FormularyRules.MaxCodeLength).Matches("^[A-Z0-9-]+$").WithMessage("must be capital letters, digits and hyphens only.");
    }
}

public sealed class UpdateFormularyMedicineCommandValidator : AbstractValidator<UpdateFormularyMedicineCommand>
{
    public UpdateFormularyMedicineCommandValidator()
    {
        FormularyValidationRules.ApplyBaseRules(this);
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public sealed class SetFormularyMedicineControlledCommandValidator : AbstractValidator<SetFormularyMedicineControlledCommand>
{
    public SetFormularyMedicineControlledCommandValidator()
    {
        RuleFor(x => x.MedicineId).NotEmpty();
        RuleFor(x => x.ControlledSchedule).MaximumLength(ControlledDrugRules.MaxScheduleLength).NoControlCharacters();
        RuleFor(x => x.Reason).MaximumLength(FormularyRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class SetFormularyMedicineActiveCommandValidator : AbstractValidator<SetFormularyMedicineActiveCommand>
{
    public SetFormularyMedicineActiveCommandValidator() => RuleFor(x => x.MedicineId).NotEmpty();
}
