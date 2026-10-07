using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Notification;

/// <summary>HO-NTF-001 §12.4 -- GET /api/admin/notifications. <c>From</c>/<c>To</c> are required
/// and capped at <c>NotificationLogRules.MaxRangeDays</c> (checked by the controller).</summary>
public sealed class GetNotificationLogQuery : Query<PagedResult<NotificationLogItemViewModel>>
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public Guid? RecipientId { get; init; }
    public string? Destination { get; init; }
    public string? Channel { get; init; }
    public string? TemplateKey { get; init; }
    public string? Status { get; init; }
}
