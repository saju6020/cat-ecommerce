using CatEcommerce.Application.Commands.Administration;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class SetMaintenanceModeCommandValidator : AbstractValidator<SetMaintenanceModeCommand>
{
    public SetMaintenanceModeCommandValidator() => RuleFor(x => x.Message).MaximumLength(500).NoControlCharacters();
}
