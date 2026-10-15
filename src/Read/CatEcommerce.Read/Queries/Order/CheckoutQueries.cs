using CatEcommerce.Domain.Models;
using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Order;

/// <summary>HO-ORD-001 §12.6 -- the checkout quote for the signed-in customer: re-prices the cart, lists every change since the items were added and carries
/// the fingerprint the customer must acknowledge (ADR-200). <b>Never prices in SQL</b>: the pure <c>CheckoutPricing</c> does.</summary>
public sealed class GetCheckoutQuoteQuery : Query<CheckoutQuoteViewModel>
{
    public required Guid CustomerId { get; init; }
    public required FulfilmentChoice Choice { get; init; }
}
