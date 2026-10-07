using CatEcommerce.Domain.Aggregates.Product;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>Shop (HO-SHP-001) DbSets. See ADR-341 for the per-area partial pattern.</summary>
public sealed partial class CatEcommerceDbContext
{
    public DbSet<ProductAggregate> Products => Set<ProductAggregate>();
    public DbSet<SkuPriceAggregate> SkuPrices => Set<SkuPriceAggregate>();
    public DbSet<SourcingOriginAggregate> SourcingOrigins => Set<SourcingOriginAggregate>();
    public DbSet<ProductSalesStatsAggregate> ProductSalesStats => Set<ProductSalesStatsAggregate>();
    public DbSet<ProductReviewAggregate> ProductReviews => Set<ProductReviewAggregate>();
    public DbSet<WishlistAggregate> Wishlists => Set<WishlistAggregate>();
    public DbSet<StockAlertAggregate> StockAlerts => Set<StockAlertAggregate>();
}
