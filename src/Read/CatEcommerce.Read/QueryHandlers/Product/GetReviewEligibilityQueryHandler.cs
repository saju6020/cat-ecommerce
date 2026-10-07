using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 12.4. Column projections only: the product's SKU rows by slug and
/// the caller's existing review id.</summary>
public sealed class GetReviewEligibilityQueryHandler(CatEcommerceDbContext db, IPurchaseHistoryReader purchaseHistoryReader)
    : IQueryHandlerAsync<GetReviewEligibilityQuery, ReviewEligibilityViewModel?>
{
    public async Task<QueryResponse<ReviewEligibilityViewModel?>> HandleAsync(GetReviewEligibilityQuery query)
    {
        // Rows 15 + 19: only an Active, listed product is known to a customer (a draft or unlisted slug is
        // indistinguishable from an unknown one), and a live product with no SKUs is a 200, not a 404.
        var productId = await db.Products.AsNoTracking()
            .Where(p => p.Slug == query.Slug && p.Status == ProductStatus.Active && p.ListedOnStorefront)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync();
        if (productId is null) return new QueryResponse<ReviewEligibilityViewModel?> { Result = null };

        var skus = await db.SkuRows().Where(r => r.ProductId == productId.Value).ToListAsync();
        var existingId = await db.ProductReviews.AsNoTracking()
            .Where(r => r.ProductId == productId.Value && r.CustomerId == query.CustomerId && r.Status != ProductReviewStatus.Withdrawn)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync();

        if (existingId is not null)
        {
            return new QueryResponse<ReviewEligibilityViewModel?>
            { Result = new ReviewEligibilityViewModel(false, "AlreadyReviewed", [], existingId) };
        }

        var purchased = await purchaseHistoryReader.GetPurchasedSkuIdsAsync(query.CustomerId, skus.Select(s => s.SkuId).ToList(), CancellationToken.None);

        var purchasedSkus = skus.Where(s => purchased.Contains(s.SkuId))
            .Select(s => new PurchasedSkuViewModel(s.SkuId, s.Label)).ToList();

        var result = new ReviewEligibilityViewModel(purchasedSkus.Count > 0, purchasedSkus.Count > 0 ? null : "NotPurchased", purchasedSkus, null);
        return new QueryResponse<ReviewEligibilityViewModel?> { Result = result };
    }
}
