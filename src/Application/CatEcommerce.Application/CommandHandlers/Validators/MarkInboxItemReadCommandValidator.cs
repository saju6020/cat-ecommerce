using CatEcommerce.Application.Commands.Notification;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class MarkInboxItemReadCommandValidator : AbstractValidator<MarkInboxItemReadCommand>
{
    public MarkInboxItemReadCommandValidator() => RuleFor(x => x.NotificationId).NotEmpty();
}
