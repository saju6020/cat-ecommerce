using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Notification;

/// <summary>
/// HO-NTF-001 SECURITY/CODE_REVIEW gate fix (2026-09-27, second pass, MEDIUM). Sent by
/// <c>NotificationWorkCycle</c>, from a FRESH scope, when dispatching
/// <see cref="ExpandNotificationRequestCommand"/> for this request threw -- i.e. the request got
/// past rendering (a render failure is suppressed synchronously, inside that same command) but the
/// save itself failed. Records the failed attempt so the request cannot retry forever and block
/// every other request behind it in the queue.
/// </summary>
public sealed class RecordNotificationExpansionFailureCommand : Command
{
    public required Guid RequestId { get; init; }
}
