using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-NTF-001 §8.6 -- input to <c>NotificationAggregate.CreateExternal</c>/<c>CreateInbox</c>.
///
/// <paramref name="ResendOrdinal"/> -- gate fix 2026-09-27 (SECURITY LOW). This resend's 1-based
/// position in its chain (<paramref name="ResendOfId"/>'s own resends); <c>null</c> for a
/// non-resend. Backs a unique filtered index on (<c>ResendOfId</c>, <c>ResendOrdinal</c>) so the
/// resend limit is enforced atomically at the database, not by a count-then-insert race in the
/// handler. Defaulted so every existing positional call site is unaffected.
/// </summary>
public sealed record NotificationCreateDto(
    Guid RequestId,
    string TemplateKey,
    int TemplateVersion,
    string Locale,
    NotificationCategory Category,
    NotificationUrgency Urgency,
    bool ContainsSecret,
    NotificationRecipientKind RecipientKind,
    Guid RecipientId,
    NotificationSubjectKind? SubjectKind,
    Guid? SubjectId,
    Guid? SubjectRef,
    DateOnly? SubjectDueOn,
    Guid? BranchId,
    NotificationChannel Channel,
    string? Destination,
    string? Subject,
    string? Title,
    string Body,
    string? HtmlBody,
    string? LinkPath,
    DateTimeOffset? NotAfter,
    DateTimeOffset CreatedOn,
    DateTimeOffset? NotBefore,
    bool DeferredForQuietHours,
    string? UnsubscribeTokenHash,
    string? UnsubscribeLink,
    Guid? FallbackFromId,
    Guid? ResendOfId,
    Guid? ResentBy,
    int? ResendOrdinal = null,
    string? SubjectStamp = null,
    NotificationAttachmentKind? AttachmentKind = null,
    Guid? AttachmentId = null);
