using CatEcommerce.Application.Commands.Cat;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class TransferCatOwnershipCommandValidator : AbstractValidator<TransferCatOwnershipCommand>
{
    public TransferCatOwnershipCommandValidator()
    {
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.NewOwnerId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
