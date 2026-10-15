using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §11.4 (C19 note), ADR-201 -- adds a <b>new</b> Order-context root to the scoped context <b>without saving</b>, exactly as <c>EfNotificationOutbox</c> does, so it
/// commits in the same save as the order that created it (a delivery failure creates its return with the order's transition). Its creation event is not dispatched
/// (V2) and nothing subscribes to it. The only new-root staging in the slice. Implemented in Infrastructure.
/// </summary>
public interface IOrderingStaging
{
    void Add<TAggregate>(TAggregate aggregate) where TAggregate : AggregateRoot;
}
