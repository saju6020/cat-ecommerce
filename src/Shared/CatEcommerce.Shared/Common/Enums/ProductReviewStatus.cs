namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 section 8.11/10.1.</summary>
public sealed class ProductReviewStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ProductReviewStatus PendingModeration = new(1, nameof(PendingModeration));
    public static readonly ProductReviewStatus Published = new(2, nameof(Published));
    public static readonly ProductReviewStatus Rejected = new(3, nameof(Rejected));
    public static readonly ProductReviewStatus Hidden = new(4, nameof(Hidden));
    public static readonly ProductReviewStatus Withdrawn = new(5, nameof(Withdrawn));
}
