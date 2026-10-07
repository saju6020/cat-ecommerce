using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Notification;

/// <summary>HO-NTF-001 §12.4 -- GET /api/admin/notifications/summary.</summary>
public sealed class GetNotificationSummaryQuery : Query<NotificationSummaryViewModel>;
