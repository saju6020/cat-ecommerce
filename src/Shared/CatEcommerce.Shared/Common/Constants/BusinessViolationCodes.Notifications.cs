namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Notifications (HO-NTF-001 §10.4) violation codes. See ADR-341 for the per-area partial
/// pattern. Do not reorder or rename another area's codes.
/// </summary>
public static partial class BusinessViolationCodes
{
    public const string TemplateNotInCatalogue = nameof(TemplateNotInCatalogue);
    public const string TemplateChannelMismatch = nameof(TemplateChannelMismatch);
    public const string MalformedTemplatePlaceholder = nameof(MalformedTemplatePlaceholder);
    public const string UnknownTemplatePlaceholder = nameof(UnknownTemplatePlaceholder);
    public const string RequiredTemplatePlaceholderMissing = nameof(RequiredTemplatePlaceholderMissing);
    public const string InvalidTemplateText = nameof(InvalidTemplateText);
    public const string InvalidTemplateLength = nameof(InvalidTemplateLength);
    public const string InvalidTemplateLink = nameof(InvalidTemplateLink);
    public const string TemplateVersionConflict = nameof(TemplateVersionConflict);
    public const string InvalidNotificationRequest = nameof(InvalidNotificationRequest);
    public const string NotificationNotFound = nameof(NotificationNotFound);
    public const string NotificationNotDue = nameof(NotificationNotDue);
    public const string NotificationCannotBeResent = nameof(NotificationCannotBeResent);
    public const string ResendLimitReached = nameof(ResendLimitReached);
    public const string ResendChannelNotAvailable = nameof(ResendChannelNotAvailable);
    public const string InvalidUnsubscribeLink = nameof(InvalidUnsubscribeLink);
    public const string PreferredChannelHasNoDestination = nameof(PreferredChannelHasNoDestination);
    public const string InvalidPreferredChannel = nameof(InvalidPreferredChannel);

    // Gate fix 2026-09-27 (SECURITY/CODE_REVIEW MEDIUM 7/LOW): shape validation that used to reach
    // the renderer or an enum parse unguarded and surface as an unhandled 500.
    public const string InvalidTemplateContents = nameof(InvalidTemplateContents);
    public const string InvalidTemplateChannel = nameof(InvalidTemplateChannel);
    public const string InvalidTemplateSampleValue = nameof(InvalidTemplateSampleValue);
}
