using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Order;

/// <summary>HO-ORD-001 §12.6 -- the caller's cart: the customer's active cart, or the guest cart named by the cookie hash. No cookie and no session
/// is an empty cart (and the controller sets no cookie).</summary>
public sealed class GetCartQuery : Query<CartViewModel>
{
    public Guid? CustomerId { get; init; }
    public string? GuestTokenHash { get; init; }
}
