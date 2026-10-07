using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 12.4 -- includes origins with no products yet.</summary>
public sealed class GetStaffOriginsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStaffOriginsQuery, IReadOnlyList<StaffOriginViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<StaffOriginViewModel>>> HandleAsync(GetStaffOriginsQuery query)
    {
        var origins = await db.SourcingOrigins.AsNoTracking()
            .Select(o => new { o.CountryCode, o.SourcingNotes, o.IsFeatured, o.SortOrder })
            .ToListAsync();

        var counts = await db.Products.AsNoTracking()
            .Where(p => p.CountryOfOriginCode != null)
            .GroupBy(p => p.CountryOfOriginCode!)
            .Select(g => new { CountryCode = g.Key, Count = g.Count() })
            .ToListAsync();

        var items = origins.Select(o => new StaffOriginViewModel(
            o.CountryCode, CountryCodes.FindName(o.CountryCode) ?? o.CountryCode, o.SourcingNotes, o.IsFeatured, o.SortOrder,
            counts.FirstOrDefault(c => c.CountryCode == o.CountryCode)?.Count ?? 0))
            .OrderBy(o => o.SortOrder).ThenBy(o => o.CountryName)
            .ToList();

        return new QueryResponse<IReadOnlyList<StaffOriginViewModel>> { Result = items };
    }
}
