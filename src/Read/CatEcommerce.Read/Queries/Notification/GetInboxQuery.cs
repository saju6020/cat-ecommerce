using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Notification;

/// <summary>HO-NTF-001 §12.4 -- GET /api/customers/me/inbox.</summary>
public sealed class GetInboxQuery : Query<PagedResult<InboxItemViewModel>>
{
    public required Guid CustomerId { get; init; }
    public bool UnreadOnly { get; init; }
}
