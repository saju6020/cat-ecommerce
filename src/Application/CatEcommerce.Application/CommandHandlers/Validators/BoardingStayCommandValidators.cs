using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-BRD-001 section 11.2 -- shape only. Availability, gate, capacity, isolation, cut-offs and completeness are aggregate or handler rules.

public sealed class CreateBoardingEnquiryCommandValidator : AbstractValidator<CreateBoardingEnquiryCommand>
{
    public CreateBoardingEnquiryCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.DepartureOn).GreaterThan(x => x.ArrivalOn);
        RuleFor(x => x.UnitType).NotEmpty().MustBeValidEnumeration<CreateBoardingEnquiryCommand, BoardingUnitType>();
        RuleFor(x => x.CatIds).NotEmpty().Must(ids => ids.Count <= StayRules.MaxCatsPerStay).WithMessage($"A booking has at most {StayRules.MaxCatsPerStay} cats.");
        RuleForEach(x => x.CatIds).NotEmpty();
    }
}

public sealed class ChangeBoardingEnquiryCommandValidator : AbstractValidator<ChangeBoardingEnquiryCommand>
{
    public ChangeBoardingEnquiryCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.DepartureOn).GreaterThan(x => x.ArrivalOn);
        RuleFor(x => x.UnitType).NotEmpty().MustBeValidEnumeration<ChangeBoardingEnquiryCommand, BoardingUnitType>();
        RuleFor(x => x.CatIds).NotEmpty().Must(ids => ids.Count <= StayRules.MaxCatsPerStay).WithMessage($"A booking has at most {StayRules.MaxCatsPerStay} cats.");
        RuleForEach(x => x.CatIds).NotEmpty();
    }
}

public sealed class SetStayCareProfileCommandValidator : AbstractValidator<SetStayCareProfileCommand>
{
    public SetStayCareProfileCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.Profile).NotNull().ChildRules(p =>
        {
            p.RuleFor(x => x.Feedings).NotEmpty().Must(f => f.Count <= CareProfileRules.MaxFeedings).WithMessage($"At most {CareProfileRules.MaxFeedings} feedings.");
            p.RuleForEach(x => x.Feedings).ChildRules(f =>
            {
                f.RuleFor(x => x.Portion).NotEmpty().MaximumLength(CareProfileRules.MaxPortionLength).NoControlCharacters();
                f.RuleFor(x => x.FoodDescription).NotEmpty().MaximumLength(CareProfileRules.MaxFoodDescriptionLength).NoControlCharacters();
                f.RuleFor(x => x.FoodSource).NotEmpty().MustBeValidEnumeration<FeedingInput, FoodSource>();
            });
            p.RuleFor(x => x.Medications).Must(m => m.Count <= CareProfileRules.MaxMedications).WithMessage($"At most {CareProfileRules.MaxMedications} medications.");
            p.RuleForEach(x => x.Medications).ChildRules(m =>
            {
                m.RuleFor(x => x.Name).NotEmpty().MaximumLength(CareProfileRules.MaxMedicationNameLength).NoControlCharacters();
                m.RuleFor(x => x.Dose).NotEmpty().MaximumLength(CareProfileRules.MaxDoseLength).NoControlCharacters();
                m.RuleFor(x => x.Route).NotEmpty().MaximumLength(CareProfileRules.MaxRouteLength).NoControlCharacters();
                m.RuleFor(x => x.Instructions).MaximumLength(CareProfileRules.MaxInstructionsLength).NoControlCharacters();
                m.RuleFor(x => x.Times).NotEmpty().Must(t => t.Count <= CareProfileRules.MaxMedicationTimes).WithMessage($"At most {CareProfileRules.MaxMedicationTimes} dose times.");
            });
            p.RuleFor(x => x.LitterType).NotEmpty().MustBeValidEnumeration<CareProfileInput, LitterType>();
            p.RuleFor(x => x.LitterNote).MaximumLength(CareProfileRules.MaxLitterNoteLength).NoControlCharacters();
            p.RuleFor(x => x.VetSpendingLimit).InclusiveBetween(0m, BillingRules.MaxAmount).MustHaveAtMostTwoDecimals();
            p.RuleFor(x => x.EmergencyContact).NotNull().ChildRules(c =>
            {
                c.RuleFor(x => x.Name).NotEmpty().MaximumLength(CareProfileRules.MaxContactNameLength).NoControlCharacters();
                c.RuleFor(x => x.Phone).NotEmpty().MaximumLength(CareProfileRules.MaxContactPhoneLength).NoControlCharacters();
                c.RuleFor(x => x.Relationship).MaximumLength(CareProfileRules.MaxContactRelationshipLength).NoControlCharacters();
            });
            p.RuleFor(x => x.Notes).MaximumLength(CareProfileRules.MaxNotesLength).NoControlCharacters();
        });
    }
}

public sealed class OverrideVaccinationGateCommandValidator : AbstractValidator<OverrideVaccinationGateCommand>
{
    public OverrideVaccinationGateCommandValidator()
    {
        RuleFor(x => x.StayId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(StayRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class ReserveBoardingStayCommandValidator : AbstractValidator<ReserveBoardingStayCommand>
{
    public ReserveBoardingStayCommandValidator() => RuleFor(x => x.StayId).NotEmpty();
}
