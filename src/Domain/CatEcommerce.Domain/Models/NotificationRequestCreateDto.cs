using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-NTF-001 §8.4 -- input to <c>NotificationRequestAggregate.Create</c>. Wraps the
/// producer contract (<see cref="NotificationRequestDto"/>) with the fields only the Notification
/// context itself knows (whether the definition carries a secret, when the request was made).</summary>
public sealed record NotificationRequestCreateDto(
    string TemplateKey,
    NotificationRecipientKind RecipientKind,
    Guid RecipientId,
    IReadOnlyDictionary<string, string> Variables,
    NotificationChannel? ChannelHint,
    NotificationSubjectKind? SubjectKind,
    Guid? SubjectId,
    Guid? SubjectRef,
    DateOnly? SubjectDueOn,
    Guid? BranchId,
    string? DeduplicationKey,
    DateTimeOffset? NotAfter,
    bool ContainsSecret,
    DateTimeOffset RequestedOn,
    Guid RequestedBy,
    UserContext UserContext,
    Guid CorrelationId,
    DateTimeOffset? SendAfter = null,
    string? SubjectStamp = null,
    NotificationAttachmentKind? AttachmentKind = null,
    Guid? AttachmentId = null);
