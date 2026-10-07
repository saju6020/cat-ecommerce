using CatEcommerce.Domain.Aggregates.Notification;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>Notifications (HO-NTF-001) DbSets. See ADR-341 for the per-area partial pattern.</summary>
public sealed partial class CatEcommerceDbContext
{
    public DbSet<NotificationTemplateAggregate> NotificationTemplates => Set<NotificationTemplateAggregate>();
    public DbSet<NotificationRequestAggregate> NotificationRequests => Set<NotificationRequestAggregate>();
    public DbSet<NotificationAggregate> Notifications => Set<NotificationAggregate>();
}
