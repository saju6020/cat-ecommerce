namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. Open until an accountant resolves it.</summary>
public sealed class ReconciliationIssueStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReconciliationIssueStatus Open = new(1, nameof(Open));
    public static readonly ReconciliationIssueStatus Resolved = new(2, nameof(Resolved));
}
