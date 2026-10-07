using CatEcommerce.Application.Commands.Cat;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class FlagCatHighRiskCommandValidator : AbstractValidator<FlagCatHighRiskCommand>
{
    public FlagCatHighRiskCommandValidator()
    {
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.HandlingNote).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
