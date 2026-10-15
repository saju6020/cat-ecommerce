using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Operations;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Operations;

/// <summary>HO-XCT-001 section 12.6 Q6 -- explicit member-by-member projection, <c>AsNoTracking</c>, paged.</summary>
public sealed class GetErasureRequestsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetErasureRequestsQuery, PagedResult<ErasureRequestViewModel>>
{
    public async Task<QueryResponse<PagedResult<ErasureRequestViewModel>>> HandleAsync(GetErasureRequestsQuery query)
    {
        var requests = db.ErasureRequests.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = Enumeration.FromDisplayName<ErasureRequestStatus>(query.Status);
            requests = requests.Where(r => r.Status == status);
        }

        var totalCount = await requests.CountAsync();

        var rows = await requests
            .OrderByDescending(r => r.RequestedOn).ThenBy(r => r.Id)
            .Skip(query.Skip).Take(query.Take)
            .Select(r => new
            {
                r.Id, r.RequestedOn, r.Source, r.Status, r.CompletedOn,
                Scopes = r.Scopes.OrderBy(s => s.ContextKey)
                    .Select(s => new { s.ContextKey, s.Status, s.HeldUntil, s.HeldBasis, s.Attempts, s.LastErrorCode, s.CompletedOn })
                    .ToList(),
            })
            .ToListAsync();

        var items = rows.Select(r => new ErasureRequestViewModel(
            r.Id, r.RequestedOn, r.Source.DisplayName, r.Status.DisplayName, r.CompletedOn,
            r.Scopes.Select(s => new ErasureScopeViewModel(s.ContextKey, s.Status.DisplayName, s.HeldUntil, s.HeldBasis, s.Attempts, s.LastErrorCode, s.CompletedOn)).ToList())).ToList();

        return new QueryResponse<PagedResult<ErasureRequestViewModel>> { Result = new PagedResult<ErasureRequestViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}

public sealed class GetErasureRequestQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetErasureRequestQuery, ErasureRequestViewModel?>
{
    public async Task<QueryResponse<ErasureRequestViewModel?>> HandleAsync(GetErasureRequestQuery query)
    {
        var row = await db.ErasureRequests.AsNoTracking()
            .Where(r => r.Id == query.CustomerAccountId)
            .Select(r => new
            {
                r.Id, r.RequestedOn, r.Source, r.Status, r.CompletedOn,
                Scopes = r.Scopes.OrderBy(s => s.ContextKey)
                    .Select(s => new { s.ContextKey, s.Status, s.HeldUntil, s.HeldBasis, s.Attempts, s.LastErrorCode, s.CompletedOn })
                    .ToList(),
            })
            .FirstOrDefaultAsync();

        ErasureRequestViewModel? result = row is null ? null : new ErasureRequestViewModel(
            row.Id, row.RequestedOn, row.Source.DisplayName, row.Status.DisplayName, row.CompletedOn,
            row.Scopes.Select(s => new ErasureScopeViewModel(s.ContextKey, s.Status.DisplayName, s.HeldUntil, s.HeldBasis, s.Attempts, s.LastErrorCode, s.CompletedOn)).ToList());

        return new QueryResponse<ErasureRequestViewModel?> { Result = result };
    }
}
