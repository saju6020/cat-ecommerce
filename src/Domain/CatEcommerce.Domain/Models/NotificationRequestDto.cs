using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// Owned by HO-NTF-001 §11.3: "the producer contract -- later handovers build it, nothing else."
/// Exact field list and order from §11.3's own code block.
///
/// <paramref name="SendAfter"/>/<paramref name="SubjectStamp"/> -- HO-APT-001 §8.9 amendment (§1
/// Amended there). A reminder is not expanded before <paramref name="SendAfter"/> (honoured by
/// <c>NotificationWorkQueue</c>); <paramref name="SubjectStamp"/> is copied onto the request and
/// compared by a subject guard (e.g. <c>AppointmentReminderGuard</c>) so a producer can invalidate
/// an already-enqueued reminder by changing the stamp on the next one, without deleting the old row.
/// Both default so every existing positional/named call site is unaffected.
/// </summary>
public sealed record NotificationRequestDto(
    Guid RequestId,
    string TemplateKey,
    NotificationRecipientKind RecipientKind,
    Guid RecipientId,
    IReadOnlyDictionary<string, string> Variables,
    NotificationChannel? ChannelHint = null,
    NotificationSubjectKind? SubjectKind = null,
    Guid? SubjectId = null,
    Guid? SubjectRef = null,
    DateOnly? SubjectDueOn = null,
    Guid? BranchId = null,
    string? DeduplicationKey = null,
    DateTimeOffset? NotAfter = null,
    Guid CorrelationId = default,
    UserContext? UserContext = null,
    DateTimeOffset? SendAfter = null,
    string? SubjectStamp = null,
    NotificationAttachmentKind? AttachmentKind = null,
    Guid? AttachmentId = null);
