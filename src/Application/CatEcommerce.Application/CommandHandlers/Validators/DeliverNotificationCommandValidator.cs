using CatEcommerce.Application.Commands.Notification;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class DeliverNotificationCommandValidator : AbstractValidator<DeliverNotificationCommand>
{
    public DeliverNotificationCommandValidator() => RuleFor(x => x.NotificationId).NotEmpty();
}
