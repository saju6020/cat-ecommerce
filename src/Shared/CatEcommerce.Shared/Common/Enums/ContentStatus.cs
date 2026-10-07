namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ADM-001 §10.1.</summary>
public sealed class ContentStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ContentStatus Draft = new(1, nameof(Draft));
    public static readonly ContentStatus Published = new(2, nameof(Published));
    public static readonly ContentStatus Unpublished = new(3, nameof(Unpublished));
}
