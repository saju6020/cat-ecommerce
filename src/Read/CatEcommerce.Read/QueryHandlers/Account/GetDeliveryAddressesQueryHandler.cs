using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Account;

/// <summary>Q-E1. Absent aggregate -> empty book (no address book has been opened yet).</summary>
public sealed class GetDeliveryAddressesQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetDeliveryAddressesQuery, DeliveryAddressBookViewModel>
{
    public async Task<QueryResponse<DeliveryAddressBookViewModel>> HandleAsync(GetDeliveryAddressesQuery query)
    {
        var row = await db.CustomerAddressBooks.AsNoTracking()
            .Where(b => b.Id == query.CustomerId)
            .Select(b => new
            {
                b.DefaultAddressId,
                Addresses = b.Addresses.Select(a => new
                {
                    a.Id, a.Label, a.Line1, a.Line2, a.City, a.District, a.PostalCode, a.DeliveryInstructions
                }).ToList()
            })
            .FirstOrDefaultAsync();

        var addresses = row?.Addresses
            .Select(a => new DeliveryAddressViewModel(
                a.Id, a.Label, a.Line1, a.Line2, a.City, a.District, a.PostalCode, a.DeliveryInstructions,
                a.Id == row.DefaultAddressId))
            .ToList()
            ?? [];

        return new QueryResponse<DeliveryAddressBookViewModel>
        {
            Result = new DeliveryAddressBookViewModel(addresses, DeliveryAddressRules.MaxAddresses)
        };
    }
}
