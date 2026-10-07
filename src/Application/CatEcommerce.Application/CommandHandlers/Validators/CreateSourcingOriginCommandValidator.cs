using CatEcommerce.Application.Commands.Product;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class CreateSourcingOriginCommandValidator : AbstractValidator<CreateSourcingOriginCommand>
{
    public CreateSourcingOriginCommandValidator()
    {
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.SourcingNotes).MaximumLength(2000);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 999);
    }
}

public sealed class UpdateSourcingOriginCommandValidator : AbstractValidator<UpdateSourcingOriginCommand>
{
    public UpdateSourcingOriginCommandValidator()
    {
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.SourcingNotes).MaximumLength(2000);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 999);
    }
}
