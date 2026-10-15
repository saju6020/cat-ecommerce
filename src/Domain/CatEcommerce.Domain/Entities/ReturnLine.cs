using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>Where a received unit went: the SKU (the component, for a bundle), the original batch and the location it was sold from, and how many.
/// Recorded at receipt so the restock decision puts it back against the <b>original batch</b> (US-ORD-14 AC2) without Ordering ever choosing one.</summary>
public sealed record ReturnReceipt(Guid SkuId, Guid BatchId, Guid LocationId, decimal Quantity);

/// <summary>
/// HO-ORD-001 §8.10 -- one line of a return: which order line, how many, why, in what condition, and (after receipt) what came back and what was decided.
/// <c>RequiresDefectConfirmation</c> is set by the eligibility policy for opened food or used litter claimed defective (BR-10): approval must then record
/// <c>DefectConfirmed</c> with a note.
/// </summary>
public sealed class ReturnLine
{
    public Guid Id { get; private set; }
    public Guid OrderLineId { get; private set; }
    public int Quantity { get; private set; }
    public ReturnReason ReasonCode { get; private set; } = default!;
    public ItemCondition Condition { get; private set; } = default!;
    public string? CustomerNote { get; private set; }
    public bool RequiresDefectConfirmation { get; private set; }
    public bool DefectConfirmed { get; private set; }
    public string? DefectNote { get; private set; }
    public int QuantityReceived { get; private set; }
    public IReadOnlyList<ReturnReceipt> Receipts { get; private set; } = [];

    private ReturnLine() { }

    public ReturnLine(
        Guid id, Guid orderLineId, int quantity, ReturnReason reasonCode, ItemCondition condition, string? customerNote, bool requiresDefectConfirmation)
    {
        Id = id;
        OrderLineId = orderLineId;
        Quantity = quantity;
        ReasonCode = reasonCode;
        Condition = condition;
        CustomerNote = customerNote;
        RequiresDefectConfirmation = requiresDefectConfirmation;
    }

    internal void ConfirmDefect(bool confirmed, string? note)
    {
        DefectConfirmed = confirmed;
        DefectNote = note;
    }

    internal void Receive(int quantity, IReadOnlyList<ReturnReceipt> receipts)
    {
        QuantityReceived = quantity;
        Receipts = receipts;
    }
}
