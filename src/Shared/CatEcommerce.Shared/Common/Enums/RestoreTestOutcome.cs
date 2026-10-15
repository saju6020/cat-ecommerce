namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-XCT-001 §10.1 -- computed by the aggregate, never supplied (ADR-313).</summary>
public sealed class RestoreTestOutcome(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RestoreTestOutcome Passed = new(1, nameof(Passed));
    public static readonly RestoreTestOutcome Failed = new(2, nameof(Failed));
}
