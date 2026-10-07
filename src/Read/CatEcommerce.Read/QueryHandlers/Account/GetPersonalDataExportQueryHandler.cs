using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Account;

/// <summary>Q-J1 / US-ACC-13. Machine-readable, versioned export.</summary>
public sealed class GetPersonalDataExportQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetPersonalDataExportQuery, PersonalDataExportViewModel?>
{
    public async Task<QueryResponse<PersonalDataExportViewModel?>> HandleAsync(GetPersonalDataExportQuery query)
    {
        var profileRow = await db.CustomerAccounts.AsNoTracking()
            .Where(c => c.Id == query.CustomerId)
            .Select(c => new
            {
                c.FullName,
                MobileNumber = c.MobileNumber == null ? null : c.MobileNumber.Value,
                EmailAddress = c.EmailAddress == null ? null : c.EmailAddress.Value,
                HasMobile = c.MobileNumber != null,
                HasEmail = c.EmailAddress != null,
                c.AdditionalPhoneNumbers,
                c.CreatedDate
            })
            .FirstOrDefaultAsync();

        if (profileRow is null) return new QueryResponse<PersonalDataExportViewModel?> { Result = null };

        var profile = new CustomerProfileViewModel(
            profileRow.FullName, profileRow.MobileNumber, profileRow.EmailAddress,
            profileRow.HasMobile, profileRow.HasEmail, profileRow.HasMobile, profileRow.AdditionalPhoneNumbers,
            new DateTimeOffset(DateTime.SpecifyKind(profileRow.CreatedDate, DateTimeKind.Utc)));

        var addressRows = await db.CustomerAddressBooks.AsNoTracking()
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

        var addresses = addressRows?.Addresses
            .Select(a => new DeliveryAddressViewModel(
                a.Id, a.Label, a.Line1, a.Line2, a.City, a.District, a.PostalCode, a.DeliveryInstructions,
                a.Id == addressRows.DefaultAddressId))
            .ToList() ?? [];

        var grants = await db.CustomerNotificationPreferences.AsNoTracking()
            .Where(p => p.Id == query.CustomerId)
            .SelectMany(p => p.MarketingConsents)
            .Select(c => new { c.Channel, c.IsGranted })
            .ToListAsync();

        var preferences = NotificationPreferenceRules.GetAll()
            .Select(cell =>
            {
                var granted = grants.FirstOrDefault(g => g.Channel == cell.Channel)?.IsGranted
                    ?? NotificationPreferenceRules.DefaultMarketingGranted;

                return new NotificationPreferenceCellViewModel(
                    cell.Channel.DisplayName, cell.Category.DisplayName,
                    NotificationPreferenceRules.Permits(cell.Channel, cell.Category, granted),
                    NotificationPreferenceRules.IsEditable(cell.Category),
                    NotificationPreferenceRules.IsAlwaysOn(cell.Category));
            })
            .ToList();

        var cats = await db.Cats.AsNoTracking()
            .Where(c => c.OwnerId == query.CustomerId)
            .Select(c => new PersonalDataExportCatViewModel(c.Id, c.Name, c.Species, c.Breed, c.Sex.DisplayName))
            .ToListAsync();

        var viewModel = new PersonalDataExportViewModel(1, timeProvider.GetUtcNow(), profile, addresses, preferences, cats);

        return new QueryResponse<PersonalDataExportViewModel?> { Result = viewModel };
    }
}
