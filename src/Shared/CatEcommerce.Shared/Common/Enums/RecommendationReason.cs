namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 section 8.13/10.1.</summary>
public sealed class RecommendationReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RecommendationReason LifeStageMatch = new(1, nameof(LifeStageMatch));
    public static readonly RecommendationReason Sterilised = new(2, nameof(Sterilised));
    public static readonly RecommendationReason WeightManagement = new(3, nameof(WeightManagement));
    public static readonly RecommendationReason ConditionMatch = new(4, nameof(ConditionMatch));
}
