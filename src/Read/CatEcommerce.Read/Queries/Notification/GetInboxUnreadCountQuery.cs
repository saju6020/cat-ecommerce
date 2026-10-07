using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Notification;

/// <summary>HO-NTF-001 §12.4 -- GET /api/customers/me/inbox/unread-count.</summary>
public sealed class GetInboxUnreadCountQuery : Query<int>
{
    public required Guid CustomerId { get; init; }
}
