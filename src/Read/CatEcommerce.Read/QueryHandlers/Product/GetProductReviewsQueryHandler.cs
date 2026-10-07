using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 12.4/12.6 -- US-SHP-09 AC3, only Published reviews. No review read
/// exposes a customer id, mobile or email (section 12.6). Author display name is derived at read
/// time; "A verified buyer" for an anonymised or merged-away account. Column projections only.</summary>
public sealed class GetProductReviewsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetProductReviewsQuery, PagedResult<ProductReviewViewModel>>
{
    public async Task<QueryResponse<PagedResult<ProductReviewViewModel>>> HandleAsync(GetProductReviewsQuery query)
    {
        var product = await db.Products.AsNoTracking()
            .Where(p => p.Slug == query.Slug && p.Status == ProductStatus.Active && p.ListedOnStorefront)
            .Select(p => new { p.Id })
            .FirstOrDefaultAsync();

        if (product is null)
            return new QueryResponse<PagedResult<ProductReviewViewModel>> { Result = new PagedResult<ProductReviewViewModel>([], query.PageNumber, query.PageSize, 0) };

        var filtered = db.ProductReviews.AsNoTracking()
            .Where(r => r.ProductId == product.Id && r.Status == ProductReviewStatus.Published);

        var totalCount = await filtered.CountAsync();

        var rows = await filtered
            .OrderByDescending(r => r.SubmittedOn)
            .Skip(query.Skip).Take(query.Take)
            .Select(r => new { r.Id, r.SkuId, r.Rating, r.Title, r.Body, r.SubmittedOn, r.CustomerId })
            .ToListAsync();

        var customerIds = rows.Select(r => r.CustomerId).Distinct().ToList();
        var customers = await db.Set<CustomerAccountAggregate>().AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.FullName, c.Status })
            .ToListAsync();

        var skuIds = rows.Select(r => r.SkuId).Distinct().ToList();
        var skus = await db.SkuRows().Where(s => skuIds.Contains(s.SkuId)).ToListAsync();

        var items = rows.Select(r =>
        {
            var customer = customers.FirstOrDefault(c => c.Id == r.CustomerId);
            var displayName = DisplayName(customer?.FullName, customer?.Status);
            var skuLabel = skus.FirstOrDefault(s => s.SkuId == r.SkuId)?.Label ?? "";

            return new ProductReviewViewModel(r.Id, r.Rating, r.Title, r.Body, displayName, skuLabel, r.SubmittedOn, true);
        }).ToList();

        return new QueryResponse<PagedResult<ProductReviewViewModel>> { Result = new PagedResult<ProductReviewViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }

    internal static string DisplayName(string? fullName, CustomerAccountStatus? status)
    {
        if (status is null || status == CustomerAccountStatus.Anonymised || status == CustomerAccountStatus.Merged || string.IsNullOrWhiteSpace(fullName))
            return "A verified buyer";

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "A verified buyer";
        if (parts.Length == 1) return parts[0];

        return $"{parts[0]} {parts[^1][0]}.";
    }
}
