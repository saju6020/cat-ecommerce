using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Owned by HO-NTF-001 §8.4 (ADR-056, ADR-057) -- the only way a producer asks for a message.
/// "The implementation (<c>EfNotificationOutbox</c>) builds the aggregate and adds it to the scoped
/// <c>DbContext</c> without saving. The producer's own repository save then commits it with the
/// business change (F23)."
///
/// Producer rules (checked by the ARCHITECTURE gate): call <see cref="Enqueue"/> in the command
/// handler, before the aggregate's save; never from a domain event handler; never pass a business
/// decision in <c>Variables</c>.
///
/// Until HO-NTF-001 lands, <c>Fakes/Notifications/InMemoryNotificationOutbox</c> records every
/// enqueued request into an in-memory list (for test assertions) and does nothing else -- there is
/// no <c>NotificationRequestAggregate</c>/table yet for it to truly enlist into (this contracts
/// pass adds no entities), and there is no business rule to deny here, so a simple in-memory
/// recorder is the right fake (docs/handover/README.md Step 1 item 3).
/// </summary>
public interface INotificationOutbox
{
    void Enqueue(NotificationRequestDto request);
}
