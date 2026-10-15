using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-SRG-001 section 11.2 -- shape only. The code format, the date order, the price rules and "sterilisation services only" are aggregate rules.</summary>
public sealed class CreateSurgeryCampaignCommandValidator : AbstractValidator<CreateSurgeryCampaignCommand>
{
    public CreateSurgeryCampaignCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(SurgeryRules.CampaignCodeMax).NoControlCharacters();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(SurgeryRules.CampaignNameMax).NoControlCharacters();
        RuleFor(x => x.Description).MaximumLength(SurgeryRules.CampaignDescriptionMax).NoControlCharacters();
        RuleFor(x => x.Prices).NotNull().Must(p => p.Count <= SurgeryRules.MaxCampaignPrices).WithMessage($"A campaign prices at most {SurgeryRules.MaxCampaignPrices} procedures.");
        RuleForEach(x => x.Prices).ChildRules(p =>
        {
            p.RuleFor(i => i.ServiceId).NotEmpty();
            p.RuleFor(i => i.Amount).GreaterThanOrEqualTo(0m).LessThan(10_000_000m);
        });
        RuleFor(x => x.BranchIds).NotNull();
    }
}

public sealed class UpdateSurgeryCampaignCommandValidator : AbstractValidator<UpdateSurgeryCampaignCommand>
{
    public UpdateSurgeryCampaignCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(SurgeryRules.CampaignNameMax).NoControlCharacters();
        RuleFor(x => x.Description).MaximumLength(SurgeryRules.CampaignDescriptionMax).NoControlCharacters();
        RuleFor(x => x.Prices).NotNull().Must(p => p.Count <= SurgeryRules.MaxCampaignPrices).WithMessage($"A campaign prices at most {SurgeryRules.MaxCampaignPrices} procedures.");
        RuleForEach(x => x.Prices).ChildRules(p =>
        {
            p.RuleFor(i => i.ServiceId).NotEmpty();
            p.RuleFor(i => i.Amount).GreaterThanOrEqualTo(0m).LessThan(10_000_000m);
        });
        RuleFor(x => x.BranchIds).NotNull();
    }
}

public sealed class SetSurgeryCampaignActiveCommandValidator : AbstractValidator<SetSurgeryCampaignActiveCommand>
{
    public SetSurgeryCampaignActiveCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
    }
}
