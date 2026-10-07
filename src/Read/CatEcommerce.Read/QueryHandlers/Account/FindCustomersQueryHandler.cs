using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Account;

/// <summary>Q-H2. Exact normalised match on the primary or an additional number, or email; <= 10 rows.</summary>
public sealed class FindCustomersQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<FindCustomersQuery, IReadOnlyList<StaffCustomerSummaryViewModel>>
{
    private const int MaxResults = 10;

    public async Task<QueryResponse<IReadOnlyList<StaffCustomerSummaryViewModel>>> HandleAsync(FindCustomersQuery query)
    {
        var filtered = db.CustomerAccounts.AsNoTracking().AsQueryable();
        var hasFilter = false;
        string? searchedMobile = null;

        if (!string.IsNullOrWhiteSpace(query.Mobile))
        {
            hasFilter = true;
            try
            {
                searchedMobile = MobileNumber.Create(query.Mobile).Value;
                var mobile = searchedMobile;
                filtered = filtered.Where(c => c.MobileNumber!.Value == mobile || c.AdditionalPhoneNumbers.Contains(mobile));
            }
            catch (DomainRuleViolationException)
            {
                return new QueryResponse<IReadOnlyList<StaffCustomerSummaryViewModel>> { Result = [] };
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Email))
        {
            hasFilter = true;
            try
            {
                var email = EmailAddress.Create(query.Email).Value;
                filtered = filtered.Where(c => c.EmailAddress!.Value == email);
            }
            catch (DomainRuleViolationException)
            {
                return new QueryResponse<IReadOnlyList<StaffCustomerSummaryViewModel>> { Result = [] };
            }
        }

        if (!hasFilter) return new QueryResponse<IReadOnlyList<StaffCustomerSummaryViewModel>> { Result = [] };

        var rows = await filtered
            .Take(MaxResults)
            .Select(c => new
            {
                c.Id, c.FullName,
                MobileNumber = c.MobileNumber == null ? null : c.MobileNumber.Value,
                EmailAddress = c.EmailAddress == null ? null : c.EmailAddress.Value,
                HasPassword = c.PasswordCredential != null,
                c.Status,
                c.AdditionalPhoneNumbers
            })
            .ToListAsync();

        var items = rows
            .Select(c => new StaffCustomerSummaryViewModel(
                c.Id, c.FullName, c.MobileNumber, c.EmailAddress,
                CustomerRecordStatusRules.Derive(c.HasPassword).DisplayName, c.Status.DisplayName,
                MobileMatchedOn: MobileMatchType(c.MobileNumber, c.AdditionalPhoneNumbers, searchedMobile)))
            .ToList();

        return new QueryResponse<IReadOnlyList<StaffCustomerSummaryViewModel>> { Result = items };
    }

    /// <summary>SECURITY gate MEDIUM: distinguishes a genuine primary-number match from an
    /// unverified additional-number claim, so the caller never treats the two the same way.</summary>
    private static string? MobileMatchType(string? mobileNumber, IReadOnlyList<string> additionalPhoneNumbers, string? searchedMobile)
    {
        if (searchedMobile is null) return null;
        if (mobileNumber == searchedMobile) return "Primary";
        if (additionalPhoneNumbers.Contains(searchedMobile)) return "Additional";
        return null;
    }
}
