using CatEcommerce.Application.Commands.Notification;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class ExpandNotificationRequestCommandValidator : AbstractValidator<ExpandNotificationRequestCommand>
{
    public ExpandNotificationRequestCommandValidator() => RuleFor(x => x.RequestId).NotEmpty();
}
