using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Read.ViewModels;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Account;

/// <summary>Q-F2.</summary>
public sealed class GetStaffAccountsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetStaffAccountsQuery, PagedResult<StaffAccountSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<StaffAccountSummaryViewModel>>> HandleAsync(GetStaffAccountsQuery query)
    {
        var filtered = db.StaffAccounts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
            filtered = filtered.Where(s => s.FullName.Contains(query.Search));

        if (!string.IsNullOrWhiteSpace(query.RoleId))
            filtered = filtered.Where(s => s.RoleIds.Contains(query.RoleId));

        var totalCount = await filtered.CountAsync();

        var items = await filtered
            .OrderBy(s => s.FullName)
            .Skip(query.Skip).Take(query.Take)
            .Select(s => new StaffAccountSummaryViewModel(
                s.Id, s.FullName,
                s.MobileNumber == null ? null : s.MobileNumber.Value,
                s.EmailAddress == null ? null : s.EmailAddress.Value,
                s.RoleIds, s.PasswordCredential != null))
            .ToListAsync();

        return new QueryResponse<PagedResult<StaffAccountSummaryViewModel>>
        {
            Result = new PagedResult<StaffAccountSummaryViewModel>(items, query.PageNumber, query.PageSize, totalCount)
        };
    }
}
