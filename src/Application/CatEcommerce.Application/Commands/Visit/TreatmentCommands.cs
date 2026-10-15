using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Visit;

// HO-EMR-001 section 11.1 C16 / C17 (Phase F, US-CLN-15). Data only; ids are minted in the controller; ValidateCommand = true (F3).

/// <summary>
/// C16 -- records a treatment on an open visit. <c>Procedure</c>: <see cref="ServiceId"/> optional, billed at that service's price when
/// <see cref="IsBillable"/>. <c>Medicine</c> / <c>Consumable</c>: <see cref="SkuId"/> required; the stock comes from the branch's clinic dispensary,
/// FEFO unless <see cref="BatchId"/> is given (then <see cref="BatchOverrideReason"/> is required when it is not the FEFO first batch -- BR-4; an
/// expired batch is refused whatever the reason -- BR-3). <see cref="AcceptShortfall"/> records the treatment even when the batch holds less than
/// <see cref="Quantity"/>: what is there is deducted and the rest is a shortfall Inventory is told about (US-CLN-15 AC2).
/// </summary>
public sealed class RecordTreatmentCommand : Command
{
    public required Guid VisitId { get; init; }
    /// <summary>The client's idempotency key and the first line's id (the controller mints one when the client sends none). A retry with the same id
    /// returns the lines already recorded and deducts nothing.</summary>
    public required Guid LineId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Kind { get; init; }

    /// <summary>Required for a procedure; for a medicine or consumable it defaults to the item name.</summary>
    public string? Description { get; init; }
    public Guid? ServiceId { get; init; }
    public Guid? SkuId { get; init; }
    public Guid? BatchId { get; init; }
    public required decimal Quantity { get; init; }
    public string? Dose { get; init; }
    public string? Route { get; init; }
    public bool IsBillable { get; init; }
    public bool AcceptShortfall { get; init; }
    public string? BatchOverrideReason { get; init; }
}

/// <summary>What <see cref="RecordTreatmentCommand"/> returns (section 12.4): <c>201 { lineId, deductedQuantity, shortfallQuantity, batch }</c> (plus the quantities unit).
/// <see cref="Batch"/> is null when nothing was deducted.</summary>
public sealed record RecordTreatmentResult(
    Guid LineId, decimal DeductedQuantity, decimal ShortfallQuantity, string Unit, TreatmentBatchResult? Batch, IReadOnlyList<TreatmentLineResult> Lines, bool Replayed);

/// <summary>One line of a request: a request that drew from several batches made one line per batch. The quantities are the line own.</summary>
public sealed record TreatmentLineResult(Guid LineId, decimal Quantity, decimal DeductedQuantity, decimal ShortfallQuantity, TreatmentBatchResult? Batch);

public sealed record TreatmentBatchResult(Guid Id, string LotNumber, DateOnly? ExpiresOn);

/// <summary>The structured detail of the <c>400 InsufficientStock</c> refusal (section 8.6 step 5): how much the chosen batch holds, in its unit,
/// returned in <c>CommandResponse.Result</c> on the failure so the client can offer "record anyway and note the shortfall".</summary>
public sealed record InsufficientStockDetail(decimal AvailableQuantity, string Unit);

/// <summary>C17 -- reverses a treatment line of an open visit. The line stays, marked <c>Reversed</c>; when stock was deducted a compensating
/// movement is committed with it.</summary>
public sealed class ReverseTreatmentLineCommand : Command
{
    public required Guid VisitId { get; init; }
    public required Guid LineId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}
