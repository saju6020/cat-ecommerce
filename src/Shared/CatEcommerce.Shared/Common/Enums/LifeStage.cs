namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 section 8.5/10.1 -- food target life stage. AllLifeStages matches every
/// life stage filter and every derived cat life stage (section 8.13).</summary>
public sealed class LifeStage(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly LifeStage Kitten = new(1, nameof(Kitten));
    public static readonly LifeStage Adult = new(2, nameof(Adult));
    public static readonly LifeStage Senior = new(3, nameof(Senior));
    public static readonly LifeStage AllLifeStages = new(4, nameof(AllLifeStages));
}
