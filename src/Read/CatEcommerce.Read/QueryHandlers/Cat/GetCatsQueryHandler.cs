using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Cat;

/// <summary>
/// Q2 -- GET /api/cats. IncludeDeceased defaults false (US-CAT-09 AC1). Paginated with the
/// base Skip/Take (F8, never redeclared).
///
/// ACCEPTANCE gate A9 fix, 2026-09-27: also selects HandlingNote, alongside the IsHighRisk this
/// handler already carried, so the list warning badge never needs a second call for the note text
/// (§4.1/HO-CAT-002 §3; HO-CAT-001 §12.3 is corrected to match, having previously specified
/// IsHighRisk alone).
/// </summary>
public sealed class GetCatsQueryHandler(
    ILogger<GetCatsQueryHandler> logger,
    CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetCatsQuery, PagedResult<CatListItemViewModel>>
{
    public async Task<QueryResponse<PagedResult<CatListItemViewModel>>> HandleAsync(GetCatsQuery query)
    {
        logger.LogInformation("Reading cats. OwnerId={OwnerId} IncludeDeceased={IncludeDeceased}", query.OwnerId, query.IncludeDeceased);
        var filtered = db.Cats.AsNoTracking().AsQueryable();

        if (query.OwnerId is not null)
            filtered = filtered.Where(c => c.OwnerId == query.OwnerId);

        if (!query.IncludeDeceased)
            filtered = filtered.Where(c => c.LifeStatus == CatLifeStatus.Alive);

        var totalCount = await filtered.CountAsync();

        // Two-phase projection: the DB-translatable phase selects plain scalars and the
        // Enumeration properties themselves (direct member access, not chained); DisplayName
        // is resolved client-side afterwards -- .ai/enumeration-pattern.md's caution about
        // chaining a member off a converted property inside a translated query.
        var rows = await filtered
            .OrderBy(c => c.Name)
            .Skip(query.Skip).Take(query.Take)
            .Select(c => new
            {
                c.Id, c.OwnerId, c.Name, c.Species, c.Breed, c.Sex, c.IsHighRisk, c.HandlingNote, c.LifeStatus
            })
            .ToListAsync();

        var items = rows
            .Select(c => new CatListItemViewModel(
                c.Id, c.OwnerId, c.Name, c.Species, c.Breed, c.Sex.DisplayName, c.IsHighRisk,
                c.HandlingNote, c.LifeStatus.DisplayName))
            .ToList();

        return new QueryResponse<PagedResult<CatListItemViewModel>>
        {
            Result = new PagedResult<CatListItemViewModel>(items, query.PageNumber, query.PageSize, totalCount)
        };
    }
}
