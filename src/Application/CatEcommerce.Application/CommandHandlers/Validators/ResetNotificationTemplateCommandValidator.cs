using CatEcommerce.Application.Commands.Notification;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-NTF-001 §11.2 C2.</summary>
public sealed class ResetNotificationTemplateCommandValidator : AbstractValidator<ResetNotificationTemplateCommand>
{
    public ResetNotificationTemplateCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(80).NoControlCharacters();
        RuleFor(x => x.ExpectedContentVersion).GreaterThanOrEqualTo(0);
    }
}
