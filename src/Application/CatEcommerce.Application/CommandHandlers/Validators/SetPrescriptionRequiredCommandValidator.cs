using CatEcommerce.Application.Commands.Product;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class SetPrescriptionRequiredCommandValidator : AbstractValidator<SetPrescriptionRequiredCommand>
{
    public SetPrescriptionRequiredCommandValidator() => RuleFor(x => x.ProductId).NotEmpty();
}
