using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Vaccination;
using CatEcommerce.Read.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Vaccination;

/// <summary>Q6 -- GET /api/vaccines.</summary>
public sealed class GetVaccinesQueryHandler(
    ILogger<GetVaccinesQueryHandler> logger,
    CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetVaccinesQuery, PagedResult<VaccineListItemViewModel>>
{
    public async Task<QueryResponse<PagedResult<VaccineListItemViewModel>>> HandleAsync(GetVaccinesQuery query)
    {
        logger.LogInformation("Reading vaccine catalogue. ActiveOnly={ActiveOnly} CoreOnly={CoreOnly}", query.ActiveOnly, query.CoreOnly);
        var filtered = db.Vaccines.AsNoTracking().AsQueryable();

        if (query.ActiveOnly)
            filtered = filtered.Where(v => v.IsActive);

        if (query.CoreOnly)
            filtered = filtered.Where(v => v.IsCore);

        var totalCount = await filtered.CountAsync();

        var items = await filtered
            .OrderBy(v => v.Name)
            .Skip(query.Skip).Take(query.Take)
            .Select(v => new VaccineListItemViewModel(v.Id, v.Code, v.Name, v.IsCore, v.ValidityMonths, v.IsActive))
            .ToListAsync();

        return new QueryResponse<PagedResult<VaccineListItemViewModel>>
        {
            Result = new PagedResult<VaccineListItemViewModel>(items, query.PageNumber, query.PageSize, totalCount)
        };
    }
}
