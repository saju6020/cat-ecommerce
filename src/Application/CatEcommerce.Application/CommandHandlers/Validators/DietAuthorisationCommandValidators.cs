using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-PHM-001 §11.2 -- shape only: ids non-empty, ValidTo not before ValidFrom, text lengths. The validity maximum, the open-visit rule and the product's prescription flag are
// aggregate or handler rules. Registered as IValidator<T> (rule 21).

public sealed class AuthoriseDietCommandValidator : AbstractValidator<AuthoriseDietCommand>
{
    public AuthoriseDietCommandValidator()
    {
        RuleFor(x => x.AuthorisationId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom);
        RuleFor(x => x.Notes).MaximumLength(DietRules.MaxNotesLength).NoControlCharacters();
    }
}

public sealed class ExtendDietAuthorisationCommandValidator : AbstractValidator<ExtendDietAuthorisationCommand>
{
    public ExtendDietAuthorisationCommandValidator() => RuleFor(x => x.AuthorisationId).NotEmpty();
}

public sealed class RevokeDietAuthorisationCommandValidator : AbstractValidator<RevokeDietAuthorisationCommand>
{
    public RevokeDietAuthorisationCommandValidator()
    {
        RuleFor(x => x.AuthorisationId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(DietRules.MaxReasonLength).NoControlCharacters();
    }
}

public sealed class ExpireDietAuthorisationCommandValidator : AbstractValidator<ExpireDietAuthorisationCommand>
{
    public ExpireDietAuthorisationCommandValidator() => RuleFor(x => x.AuthorisationId).NotEmpty();
}
