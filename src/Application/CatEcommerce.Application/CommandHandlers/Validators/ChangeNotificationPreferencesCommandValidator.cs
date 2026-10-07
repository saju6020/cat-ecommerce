using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>
/// HO-NTF-001 §8.5/§11.1 amendment: <c>Changes</c> may now be empty when <c>PreferredChannel</c>
/// alone is supplied -- at least one of the two must be present.
/// </summary>
public sealed class ChangeNotificationPreferencesCommandValidator : AbstractValidator<ChangeNotificationPreferencesCommand>
{
    public ChangeNotificationPreferencesCommandValidator()
    {
        RuleFor(x => x).Must(x => x.Changes.Count > 0 || !string.IsNullOrEmpty(x.PreferredChannel))
            .WithMessage("At least one of Changes or PreferredChannel is required.");

        RuleForEach(x => x.Changes).ChildRules(change =>
        {
            change.RuleFor(c => c.Channel).NotEmpty();
            change.RuleFor(c => c.Category).NotEmpty();
        });

        RuleFor(x => x.PreferredChannel).MustBeValidEnumeration<ChangeNotificationPreferencesCommand, NotificationChannel>()
            .When(x => !string.IsNullOrEmpty(x.PreferredChannel));
    }
}
