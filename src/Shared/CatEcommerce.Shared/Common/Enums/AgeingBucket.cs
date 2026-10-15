namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §10.1. Computed only (<c>AgeingCalculator</c>) -- never persisted.</summary>
public sealed class AgeingBucket(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AgeingBucket Current = new(1, nameof(Current));
    public static readonly AgeingBucket Days1To30 = new(2, nameof(Days1To30));
    public static readonly AgeingBucket Days31To60 = new(3, nameof(Days31To60));
    public static readonly AgeingBucket Days61To90 = new(4, nameof(Days61To90));
    public static readonly AgeingBucket Over90 = new(5, nameof(Over90));
}
