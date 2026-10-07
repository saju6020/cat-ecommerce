using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-NTF-001 §11.2 C1 -- shape only. Placeholder and markup rules are <c>TemplateRules</c>,
/// called by the aggregate (needs the catalogue definition).</summary>
public sealed class SaveNotificationTemplateCommandValidator : AbstractValidator<SaveNotificationTemplateCommand>
{
    private static readonly System.Text.RegularExpressions.Regex KeyPattern = new(@"^[a-z]+(\.[a-z0-9-]+)+$");

    public SaveNotificationTemplateCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(80).Matches(KeyPattern).NoControlCharacters();
        RuleFor(x => x.ExpectedContentVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Contents).NotEmpty().Must(c => c.Count <= 4).WithMessage("A template has at most 4 channels.");

        // Gate fix 2026-09-27 (SECURITY LOW): a null entry in Contents (`contents:[null]`) used to
        // reach FluentValidation's ChildRules unguarded and surface as an unhandled 500. Rejected
        // by name here, and excluded from the per-item rules below via .Where so they never see it.
        RuleFor(x => x.Contents).Must(c => c.All(item => item is not null))
            .WithMessage("Contents must not contain a null entry.");

        RuleForEach(x => x.Contents).Where(c => c is not null).ChildRules(content =>
        {
            content.RuleFor(c => c.Channel).NotEmpty().MustBeValidEnumeration<NotificationTemplateContentItem, NotificationChannel>();
            content.RuleFor(c => c.Body).NotEmpty().NoControlCharacters().NoPrivateUseOrFormatCharacters();
            content.RuleFor(c => c.Subject).NoControlCharacters().NoPrivateUseOrFormatCharacters();
            content.RuleFor(c => c.Title).NoControlCharacters().NoPrivateUseOrFormatCharacters();
            content.RuleFor(c => c.LinkPath).NoControlCharacters().NoPrivateUseOrFormatCharacters();
        });
    }
}
