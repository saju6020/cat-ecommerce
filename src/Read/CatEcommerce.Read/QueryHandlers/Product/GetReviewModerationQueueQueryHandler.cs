using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 12.4 -- staff moderation queue. Column projections only.</summary>
public sealed class GetReviewModerationQueueQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetReviewModerationQueueQuery, PagedResult<ReviewModerationItemViewModel>>
{
    public async Task<QueryResponse<PagedResult<ReviewModerationItemViewModel>>> HandleAsync(GetReviewModerationQueueQuery query)
    {
        var filtered = db.ProductReviews.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status) && EnumerationParsing.TryParse<ProductReviewStatus>(query.Status, out var status))
            filtered = filtered.Where(r => r.Status == status);

        if (query.ProductId is { } productId)
            filtered = filtered.Where(r => r.ProductId == productId);

        var totalCount = await filtered.CountAsync();

        var rows = await filtered
            .OrderBy(r => r.SubmittedOn)
            .Skip(query.Skip).Take(query.Take)
            .Select(r => new { r.Id, r.ProductId, r.SkuId, r.Rating, r.Title, r.Body, r.SubmittedOn, r.CustomerId, r.Status })
            .ToListAsync();

        var skuIds = rows.Select(r => r.SkuId).Distinct().ToList();
        var skus = await db.SkuRows().Where(s => skuIds.Contains(s.SkuId)).ToListAsync();

        var customerIds = rows.Select(r => r.CustomerId).Distinct().ToList();
        var customers = await db.Set<CustomerAccountAggregate>().AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.FullName })
            .ToListAsync();

        var items = rows.Select(r =>
        {
            var sku = skus.FirstOrDefault(s => s.SkuId == r.SkuId);
            var customerName = customers.FirstOrDefault(c => c.Id == r.CustomerId)?.FullName ?? r.CustomerId.ToString();

            return new ReviewModerationItemViewModel(r.Id, r.ProductId, sku?.ProductName ?? "", sku?.Label ?? "", r.Rating, r.Title, r.Body, customerName, r.SubmittedOn, r.Status.DisplayName);
        }).ToList();

        return new QueryResponse<PagedResult<ReviewModerationItemViewModel>> { Result = new PagedResult<ReviewModerationItemViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}
