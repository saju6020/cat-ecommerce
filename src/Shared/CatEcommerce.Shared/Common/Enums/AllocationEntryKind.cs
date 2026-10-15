namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §8.8 / §10.1. Allocation entries are append-only (ADR-185).</summary>
public sealed class AllocationEntryKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AllocationEntryKind Allocate = new(1, nameof(Allocate));
    public static readonly AllocationEntryKind Release = new(2, nameof(Release));
}
