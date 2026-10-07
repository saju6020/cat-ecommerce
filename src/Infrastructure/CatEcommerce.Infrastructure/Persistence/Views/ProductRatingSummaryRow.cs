namespace CatEcommerce.Infrastructure.Persistence.Views;

/// <summary>Maps <c>product.product_rating_summary</c> (section 12.1, keyless, <c>ToView</c>).</summary>
public sealed class ProductRatingSummaryRow
{
    public Guid ProductId { get; init; }
    public decimal RatingAverage { get; init; }
    public int RatingCount { get; init; }
}
