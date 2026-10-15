namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-XCT-001 §10.1.</summary>
public sealed class RetentionRunTrigger(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RetentionRunTrigger Scheduled = new(1, nameof(Scheduled));
    public static readonly RetentionRunTrigger Manual = new(2, nameof(Manual));
}
