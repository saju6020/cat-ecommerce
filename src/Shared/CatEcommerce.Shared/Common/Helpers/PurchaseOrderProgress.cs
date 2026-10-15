using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>One PO line's derived receipt progress (ADR-112).</summary>
public sealed record PurchaseOrderLineProgress(decimal Ordered, decimal Accepted, decimal Rejected, decimal Outstanding);

/// <summary>
/// HO-IMP-001 §8.5 / ADR-112 -- receipt progress is DERIVED from posted receipt lines, never stored, so receiving never
/// edits the PO (rule 1) and there is no counter to drift. <c>outstanding = max(0, ordered - accepted)</c>: a rejected
/// unit is still owed (OQ-9).
/// </summary>
public static class PurchaseOrderProgress
{
    public static PurchaseOrderLineProgress ForLine(decimal ordered, decimal accepted, decimal rejected) =>
        new(ordered, accepted, rejected, Math.Max(0m, ordered - accepted));

    public static PurchaseOrderReceiptState StateOf(IReadOnlyCollection<PurchaseOrderLineProgress> lines)
    {
        if (lines.Count == 0 || lines.All(l => l.Accepted == 0m && l.Rejected == 0m)) return PurchaseOrderReceiptState.NotReceived;
        return lines.All(l => l.Outstanding == 0m) ? PurchaseOrderReceiptState.FullyReceived : PurchaseOrderReceiptState.PartiallyReceived;
    }
}
