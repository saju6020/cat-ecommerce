using CatEcommerce.Application.Commands.Cat;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class ClearCatHighRiskFlagCommandValidator : AbstractValidator<ClearCatHighRiskFlagCommand>
{
    public ClearCatHighRiskFlagCommandValidator()
    {
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
