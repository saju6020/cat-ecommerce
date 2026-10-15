namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-XCT-001 §10.1 -- one context's progress through an erasure request.</summary>
public sealed class ErasureScopeStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ErasureScopeStatus Pending = new(1, nameof(Pending));
    public static readonly ErasureScopeStatus Held = new(2, nameof(Held));
    public static readonly ErasureScopeStatus Completed = new(3, nameof(Completed));
    public static readonly ErasureScopeStatus Failing = new(4, nameof(Failing));
}
