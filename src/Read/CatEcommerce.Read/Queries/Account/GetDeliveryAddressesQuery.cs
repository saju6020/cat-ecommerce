using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Account;

/// <summary>Q-E1 -- GET /api/customers/me/addresses.</summary>
public sealed class GetDeliveryAddressesQuery : Query<DeliveryAddressBookViewModel>
{
    public required Guid CustomerId { get; init; }
}
