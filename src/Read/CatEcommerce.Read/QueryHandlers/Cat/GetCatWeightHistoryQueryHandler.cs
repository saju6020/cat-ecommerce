using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Read.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Cat;

/// <summary>Q3 -- GET /api/cats/{catId}/weights. The series behind US-CAT-02 AC2's chart.</summary>
public sealed class GetCatWeightHistoryQueryHandler(
    ILogger<GetCatWeightHistoryQueryHandler> logger,
    CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetCatWeightHistoryQuery, PagedResult<CatWeightPointViewModel>>
{
    public async Task<QueryResponse<PagedResult<CatWeightPointViewModel>>> HandleAsync(
        GetCatWeightHistoryQuery query)
    {
        logger.LogInformation("Reading weight history for cat {CatId}", query.CatId);

        var filtered = db.CatWeightRecords.AsNoTracking().Where(w => w.CatId == query.CatId);

        var totalCount = await filtered.CountAsync();

        var items = await filtered
            .OrderBy(w => w.RecordedOn)
            .Skip(query.Skip).Take(query.Take)
            .Select(w => new CatWeightPointViewModel(w.RecordedOn, w.Weight.Kilograms))
            .ToListAsync();

        return new QueryResponse<PagedResult<CatWeightPointViewModel>>
        {
            Result = new PagedResult<CatWeightPointViewModel>(items, query.PageNumber, query.PageSize, totalCount)
        };
    }
}
