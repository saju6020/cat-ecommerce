using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 12.4/12.6 -- UI-4. Filtering, sorting and paging happen entirely in
/// SQL.</summary>
public sealed class GetStaffProductsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetStaffProductsQuery, PagedResult<StaffProductSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<StaffProductSummaryViewModel>>> HandleAsync(GetStaffProductsQuery query)
    {
        var filtered = db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim().ToLowerInvariant();
            filtered = filtered.Where(p => p.SearchText.Contains(term) || p.Name.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && EnumerationParsing.TryParse<CatEcommerce.Shared.Common.Enums.ProductStatus>(query.Status, out var status))
            filtered = filtered.Where(p => p.Status == status);

        if (!string.IsNullOrWhiteSpace(query.Category) && EnumerationParsing.TryParse<ProductCategory>(query.Category, out var category))
            filtered = filtered.Where(p => p.Category == category);

        if (!string.IsNullOrWhiteSpace(query.Brand))
            filtered = filtered.Where(p => p.Brand == query.Brand);

        if (!string.IsNullOrWhiteSpace(query.Kind) && EnumerationParsing.TryParse<ProductKind>(query.Kind, out var kind))
            filtered = filtered.Where(p => p.Kind == kind);

        if (query.Listed is { } listed)
            filtered = filtered.Where(p => p.ListedOnStorefront == listed);

        if (!string.IsNullOrWhiteSpace(query.Barcode))
            filtered = filtered.Where(p => p.Skus.Any(s => s.Barcode == query.Barcode));

        var totalCount = await filtered.CountAsync();

        filtered = query.Sort switch
        {
            "name_desc" => filtered.OrderByDescending(p => p.Name),
            _ => filtered.OrderBy(p => p.Name)
        };

        // Two-phase (matches GetCustomerProfileQueryHandler): LastUpdatedDate is a platform DateTime
        // column; the DateTimeOffset conversion happens client-side after the DB-translatable phase.
        var rows = await filtered
            .Skip(query.Skip).Take(query.Take)
            .Select(p => new
            {
                p.Id, p.Name, p.Brand, Category = p.Category.DisplayName, Status = p.Status.DisplayName,
                Kind = p.Kind.DisplayName, SkuCount = p.Skus.Count, p.ListedOnStorefront, p.LastUpdatedDate
            })
            .ToListAsync();

        var items = rows.Select(r => new StaffProductSummaryViewModel(
            r.Id, r.Name, r.Brand, r.Category, r.Status, r.Kind, r.SkuCount, r.ListedOnStorefront,
            new DateTimeOffset(DateTime.SpecifyKind(r.LastUpdatedDate, DateTimeKind.Utc)))).ToList();

        return new QueryResponse<PagedResult<StaffProductSummaryViewModel>>
        {
            Result = new PagedResult<StaffProductSummaryViewModel>(items, query.PageNumber, query.PageSize, totalCount)
        };
    }
}
