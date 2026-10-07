using {{RootNamespace}}.Application.Commands.[ContextName];
using FluentValidation;

namespace {{RootNamespace}}.Application.CommandHandlers.Validators;

/// <summary>
/// Template for a Command Validator (shape/input validation only — cross-entity checks such as
/// uniqueness belong in the CommandService, not here). Working reference:
/// src/Application/Bits.Procurement.Application/CommandHandlers/Validators/RegisterVendorCommandValidator.cs
///
/// Plain FluentValidation AbstractValidator&lt;TCommand&gt; — there is no AbstractCommandValidator
/// base class and no GetEventMessage/WithState hook in this codebase. Register this as
/// IValidator&lt;[ActionName]Command&gt; in every host that needs it (Api for shape validation at
/// the controller, CommandWorker to run the handler) — see .ai/di-registration-guide.md and
/// .ai/host-composition.md. The platform's IValidationService/FluentValidationProvider resolves it
/// from DI on its own; nothing in the handler wires it up.
/// </summary>
public sealed class [ActionName]CommandValidator : AbstractValidator<[ActionName]Command>
{
    public [ActionName]CommandValidator()
    {
        RuleFor(x => x.[EntityName]Id)
            .NotEmpty().WithMessage("[EntityName]Id is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        // Example of conditional validation:
        // When(x => x.IsActive, () =>
        // {
        //     RuleFor(x => x.StartDate)
        //         .NotEmpty().WithMessage("Start date is required when active.");
        // });
    }
}
