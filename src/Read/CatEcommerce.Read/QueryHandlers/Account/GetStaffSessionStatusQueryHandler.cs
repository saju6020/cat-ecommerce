using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Read.ViewModels;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Account;

/// <summary>
/// Q-G1 / §12.5. EndedOn is null, ExpiresOn > now and the security-stamp match are filtered in
/// SQL, same as Q-C1. Idle-vs-active classification is left to the caller
/// (<c>SessionStateRules.Classify</c>) using the raw <see cref="StaffSessionStatusViewModel.LastActivityOn"/>
/// and <see cref="StaffSessionStatusViewModel.IdleTimeoutMinutes"/> returned here, so the
/// aggregate and the read side cannot disagree about what "idle" means.
/// </summary>
public sealed class GetStaffSessionStatusQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetStaffSessionStatusQuery, StaffSessionStatusViewModel?>
{
    public async Task<QueryResponse<StaffSessionStatusViewModel?>> HandleAsync(GetStaffSessionStatusQuery query)
    {
        var row = await (
            from s in db.StaffSessions.AsNoTracking()
            join staff in db.StaffAccounts.AsNoTracking() on s.StaffAccountId equals staff.Id
            where s.TokenHash == query.TokenHash
                  && s.EndedOn == null
                  && s.ExpiresOn > query.AsOf
                  && s.IssuedForSecurityStamp == staff.SecurityStamp
            select new { staff.Id, staff.FullName, staff.RoleIds, s.IssuedOn, s.LastActivityOn, s.ExpiresOn, s.CsrfTokenHash }
        ).FirstOrDefaultAsync();

        if (row is null) return new QueryResponse<StaffSessionStatusViewModel?> { Result = null };

        var viewModel = new StaffSessionStatusViewModel(
            row.Id, row.FullName, row.RoleIds, row.IssuedOn, row.LastActivityOn,
            IdleExpiresOn: row.LastActivityOn.AddMinutes(query.IdleTimeoutMinutes),
            AbsoluteExpiresOn: row.ExpiresOn,
            IdleTimeoutMinutes: query.IdleTimeoutMinutes,
            ServerTime: query.AsOf)
        {
            CsrfTokenHash = row.CsrfTokenHash
        };

        return new QueryResponse<StaffSessionStatusViewModel?> { Result = viewModel };
    }
}
