using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;

namespace CatEcommerce.Infrastructure.Notifications;

/// <summary>HO-NTF-001 §8.1/ADR-056 -- adds to the scoped <see cref="CatEcommerceDbContext"/>
/// without saving. See <see cref="INotificationEnlister"/> for why this exists.</summary>
public sealed class EfNotificationEnlister(CatEcommerceDbContext db) : INotificationEnlister
{
    public void Enlist(NotificationAggregate notification) => db.Add(notification);
    public void Enlist(AuditEntryAggregate auditEntry) => db.Add(auditEntry);
}
