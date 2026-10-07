using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Account;

/// <summary>
/// Q-C1 / §12.5. EndedOn is null, ExpiresOn > now and the security-stamp match are all filtered
/// in SQL, not re-checked in C# after materialising -- the middleware and this query must not be
/// able to disagree about what "valid" means.
/// </summary>
public sealed class GetCurrentCustomerSessionQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetCurrentCustomerSessionQuery, CustomerSessionViewModel?>
{
    public async Task<QueryResponse<CustomerSessionViewModel?>> HandleAsync(GetCurrentCustomerSessionQuery query)
    {
        var row = await (
            from s in db.CustomerSessions.AsNoTracking()
            join c in db.CustomerAccounts.AsNoTracking() on s.CustomerAccountId equals c.Id
            where s.TokenHash == query.TokenHash
                  && s.EndedOn == null
                  && s.ExpiresOn > query.AsOf
                  && s.IssuedForSecurityStamp == c.SecurityStamp
            select new { c.Id, c.FullName, s.ExpiresOn, s.CsrfTokenHash, HasPassword = c.PasswordCredential != null }
        ).FirstOrDefaultAsync();

        if (row is null) return new QueryResponse<CustomerSessionViewModel?> { Result = null };

        var viewModel = new CustomerSessionViewModel(
            row.Id, row.FullName, row.ExpiresOn, CustomerRecordStatusRules.Derive(row.HasPassword).DisplayName)
        {
            CsrfTokenHash = row.CsrfTokenHash
        };

        return new QueryResponse<CustomerSessionViewModel?> { Result = viewModel };
    }
}
