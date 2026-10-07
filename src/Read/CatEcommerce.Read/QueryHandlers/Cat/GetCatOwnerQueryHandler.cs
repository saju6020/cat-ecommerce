using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Cat;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Cat;

/// <summary>HO-ADM-001 §12.3.</summary>
public sealed class GetCatOwnerQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetCatOwnerQuery, Guid?>
{
    public async Task<QueryResponse<Guid?>> HandleAsync(GetCatOwnerQuery query)
    {
        var ownerId = await db.Cats.AsNoTracking()
            .Where(c => c.Id == query.CatId)
            .Select(c => (Guid?)c.OwnerId)
            .FirstOrDefaultAsync();

        return new QueryResponse<Guid?> { Result = ownerId };
    }
}
