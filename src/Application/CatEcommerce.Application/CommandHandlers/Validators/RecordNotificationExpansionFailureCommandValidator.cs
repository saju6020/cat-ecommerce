using CatEcommerce.Application.Commands.Notification;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RecordNotificationExpansionFailureCommandValidator : AbstractValidator<RecordNotificationExpansionFailureCommand>
{
    public RecordNotificationExpansionFailureCommandValidator() => RuleFor(x => x.RequestId).NotEmpty();
}
