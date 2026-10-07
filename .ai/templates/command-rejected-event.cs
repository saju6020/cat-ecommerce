using {{RootNamespace}}.Shared.Common.Constants;
using Platform.Infrastructure.Core.Events;
using Platform.Infrastructure.Core.Notification;

namespace {{RootNamespace}}.Domain.Events.ErrorEvents;

/// <summary>
/// Template for a Command Rejection Event, one per aggregate context. Working reference:
/// src/Domain/Bits.Procurement.Domain/Events/ErrorEvents/VendorCommandRejectedEvent.cs.
///
/// ADR-005 (.ai/decisions/adr.md): a command handler runs in CommandWorker after the Api has
/// already returned 202 Accepted (ADR-004), so a business rule violation has no HTTP response body
/// left to carry it back. The rejecting handler publishes this event instead, via
/// IBusMessageDispatcher.PublishAsync (events are published, commands are sent — ADR-004), in
/// addition to returning CommandResponse { IsSuccess = false }, not instead of it.
///
/// This does NOT extend Platform.Infrastructure.Core.Events.BusinessRuleViolatedEvent — that type
/// is real but carries no notification transport. This event implements INotifiedByEventHandler
/// directly so it reaches the client through the same declarative notification mechanism
/// (NotificationHandlerMiddleware) as success-path events like [EntityName]CreatedEvent.
///
/// Not projected: it carries no state a read model needs, only a notification. Do not register an
/// EventConsumerAdapter for it in EventWorker and do not write a ViewModel projection for it.
///
/// The violation text is exposed as ViolationMessage, never Message — INotificationMessage (via
/// INotifiedByEventHandler) already declares Message as
/// IEnumerable&lt;KeyValuePair&lt;string, object&gt;&gt;; a string Message would collide with it.
/// </summary>
public sealed class [ContextName]CommandRejectedEvent : Event, INotifiedByEventHandler
{
    public Guid [EntityName]Id { get; init; }

    /// <summary>E.g. "Create", "Update", "Blacklist" — the mutation that was rejected.</summary>
    public string ActionName { get; init; } = string.Empty;

    /// <summary>From BusinessViolationCodes.</summary>
    public string ViolationCode { get; init; } = string.Empty;

    public string ViolationMessage { get; init; } = string.Empty;

    public string? PropertyName { get; init; }

    // --- INotificationMessage (via INotifiedByEventHandler) ---
    // Setters are no-ops: every value here is derived from the domain fields above rather than
    // assigned independently, mirroring [EntityName]CreatedEvent/VendorCommandRejectedEvent.

    public IEnumerable<KeyValuePair<string, object>> Message
    {
        get => new[]
        {
            new KeyValuePair<string, object>(nameof([EntityName]Id), [EntityName]Id),
            new KeyValuePair<string, object>(nameof(ActionName), ActionName),
            new KeyValuePair<string, object>(nameof(ViolationCode), ViolationCode),
            new KeyValuePair<string, object>(nameof(ViolationMessage), ViolationMessage),
            new KeyValuePair<string, object>(nameof(CorrelationId), CorrelationId)
        };
        set { }
    }

    public string Topic
    {
        get => NotificationKeys.[ContextName]CommandRejected;
        set { }
    }

    /// <summary>Notify the user who submitted the command — the only identity available here.</summary>
    public Guid[] UserIds
    {
        get => UserContext is { UserId: var userId } && userId != Guid.Empty ? [userId] : [];
        set { }
    }

    public string[] Roles { get => []; set { } }
    public Guid[] SessionIds { get => []; set { } }
    public string DeviceType { get => string.Empty; set { } }
    public bool Offline { get => false; set { } }
    public Guid[] ApplicatonIds { get => []; set { } }
}
