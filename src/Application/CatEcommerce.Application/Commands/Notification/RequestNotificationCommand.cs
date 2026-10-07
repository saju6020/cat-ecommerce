using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Notification;

/// <summary>HO-NTF-001 §11.1 C3 -- the <c>NotificationRequestDto</c> fields plus <c>RequestId</c>.
/// Sent by timed producers and tests; a real business command handler calls
/// <c>INotificationOutbox.Enqueue</c> directly (§8.4) rather than dispatching this command.</summary>
public sealed class RequestNotificationCommand : Command
{
    public required Guid RequestId { get; init; }
    public required string TemplateKey { get; init; }
    public required string RecipientKind { get; init; }
    public required Guid RecipientId { get; init; }
    public required IReadOnlyDictionary<string, string> Variables { get; init; }
    public string? ChannelHint { get; init; }
    public string? SubjectKind { get; init; }
    public Guid? SubjectId { get; init; }
    public Guid? SubjectRef { get; init; }
    public DateOnly? SubjectDueOn { get; init; }
    public Guid? BranchId { get; init; }
    public string? DeduplicationKey { get; init; }
    public DateTimeOffset? NotAfter { get; init; }
}
