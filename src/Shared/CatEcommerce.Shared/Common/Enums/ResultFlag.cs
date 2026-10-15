namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-EMR-001 §10.1.</summary>
public sealed class ResultFlag(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ResultFlag Normal = new(1, nameof(Normal));
    public static readonly ResultFlag Low = new(2, nameof(Low));
    public static readonly ResultFlag High = new(3, nameof(High));
    public static readonly ResultFlag Abnormal = new(4, nameof(Abnormal));
    public static readonly ResultFlag NotAssessed = new(5, nameof(NotAssessed));
}
