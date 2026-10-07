using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-NTF-001 §11.2 C3 -- shape only. The definition, subject and dedup-race checks are
/// the handler's job (§11.4).</summary>
public sealed class RequestNotificationCommandValidator : AbstractValidator<RequestNotificationCommand>
{
    public RequestNotificationCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.TemplateKey).NotEmpty().MaximumLength(80).NoControlCharacters();
        RuleFor(x => x.RecipientKind).NotEmpty().MustBeValidEnumeration<RequestNotificationCommand, NotificationRecipientKind>();
        RuleFor(x => x.RecipientId).NotEmpty();
        RuleFor(x => x.Variables).Must(v => v.Count <= NotificationRequestRules.MaxVariables);
        RuleForEach(x => x.Variables.Values).MaximumLength(NotificationRequestRules.MaxVariableLength);
        RuleFor(x => x.ChannelHint).MustBeValidEnumeration<RequestNotificationCommand, NotificationChannel>().When(x => x.ChannelHint is not null);
        RuleFor(x => x.SubjectKind).MustBeValidEnumeration<RequestNotificationCommand, NotificationSubjectKind>().When(x => x.SubjectKind is not null);
        RuleFor(x => x.DeduplicationKey).MaximumLength(NotificationRequestRules.MaxDeduplicationKeyLength).NoControlCharacters();
    }
}
