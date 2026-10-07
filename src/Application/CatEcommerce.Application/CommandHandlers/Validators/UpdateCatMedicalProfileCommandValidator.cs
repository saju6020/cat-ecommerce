using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>
/// SECURITY gate, LOW (2026-09-26, second pass): each list is now capped at
/// <see cref="CatMedicalProfileRules.MaxListItems"/> -- see that class's own comment for why the
/// incidental cap the old audit column length used to provide is gone now that
/// <c>audit_entry.before_value</c>/<c>after_value</c> are unbounded <c>text</c> (proven live: 3,000
/// allergies of 500 characters each, about 1.5 MB, succeeded in about 1.7 seconds).
/// </summary>
public sealed class UpdateCatMedicalProfileCommandValidator : AbstractValidator<UpdateCatMedicalProfileCommand>
{
    public UpdateCatMedicalProfileCommandValidator()
    {
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.DietaryNotes).MaximumLength(1000);
        RuleFor(x => x.BehaviouralNotes).MaximumLength(1000);

        RuleFor(x => x.Allergies).Must(l => l.Count <= CatMedicalProfileRules.MaxListItems)
            .WithMessage($"No more than {CatMedicalProfileRules.MaxListItems} allergies are allowed.");
        RuleForEach(x => x.Allergies).ChildRules(allergy =>
        {
            allergy.RuleFor(a => a.Allergen).NotEmpty().MaximumLength(200);
            allergy.RuleFor(a => a.Reaction).NotEmpty().MaximumLength(500);
        });

        RuleFor(x => x.ChronicConditions).Must(l => l.Count <= CatMedicalProfileRules.MaxListItems)
            .WithMessage($"No more than {CatMedicalProfileRules.MaxListItems} chronic conditions are allowed.");
        RuleForEach(x => x.ChronicConditions).ChildRules(condition =>
        {
            condition.RuleFor(c => c.Condition).NotEmpty().MaximumLength(200);
            condition.RuleFor(c => c.Notes).MaximumLength(1000);
        });

        RuleFor(x => x.Medications).Must(l => l.Count <= CatMedicalProfileRules.MaxListItems)
            .WithMessage($"No more than {CatMedicalProfileRules.MaxListItems} medications are allowed.");
        RuleForEach(x => x.Medications).ChildRules(medication =>
        {
            medication.RuleFor(m => m.MedicationName).NotEmpty().MaximumLength(200);
            medication.RuleFor(m => m.Dosage).NotEmpty().MaximumLength(100);
            medication.RuleFor(m => m.Frequency).NotEmpty().MaximumLength(100);
        });
    }
}
