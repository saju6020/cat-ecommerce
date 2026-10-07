using CatEcommerce.Application.Commands.Administration;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class ChangeBusinessSettingsCommandValidator : AbstractValidator<ChangeBusinessSettingsCommand>
{
    public ChangeBusinessSettingsCommandValidator()
    {
        RuleFor(x => x.Changes).NotEmpty();
        RuleForEach(x => x.Changes).ChildRules(c =>
        {
            c.RuleFor(i => i.Key).NotEmpty();
            c.RuleFor(i => i.Value).NotNull();
        });
    }
}

public sealed class ClearBranchSettingOverrideCommandValidator : AbstractValidator<ClearBranchSettingOverrideCommand>
{
    public ClearBranchSettingOverrideCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Key).NotEmpty();
    }
}
