namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 §8.3.1/§10.1. A SKU is discontinued, never deleted (ADR-079).</summary>
public sealed class SkuStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly SkuStatus Active = new(1, nameof(Active));
    public static readonly SkuStatus Discontinued = new(2, nameof(Discontinued));
}
