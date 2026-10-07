using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 12.4. Column projections only (reviews, then the SKU rows they name).</summary>
public sealed class GetMyReviewsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetMyReviewsQuery, IReadOnlyList<MyReviewViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<MyReviewViewModel>>> HandleAsync(GetMyReviewsQuery query)
    {
        var reviews = await db.ProductReviews.AsNoTracking()
            .Where(r => r.CustomerId == query.CustomerId)
            .OrderByDescending(r => r.SubmittedOn)
            .Select(r => new { r.Id, r.ProductId, r.SkuId, r.Rating, r.Title, r.Body, r.Status, r.SubmittedOn })
            .ToListAsync();

        var skuIds = reviews.Select(r => r.SkuId).Distinct().ToList();
        var skus = await db.SkuRows().Where(s => skuIds.Contains(s.SkuId)).ToListAsync();

        IReadOnlyList<MyReviewViewModel> items = reviews.Select(r =>
        {
            var sku = skus.FirstOrDefault(s => s.SkuId == r.SkuId);
            return new MyReviewViewModel(r.Id, r.ProductId, sku?.ProductName ?? "", sku?.Label ?? "", r.Rating, r.Title, r.Body, r.Status.DisplayName, r.SubmittedOn);
        }).ToList();

        return new QueryResponse<IReadOnlyList<MyReviewViewModel>> { Result = items };
    }
}
