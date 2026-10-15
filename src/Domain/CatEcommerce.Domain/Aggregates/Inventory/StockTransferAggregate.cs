using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>
/// HO-IMP-001 §8.10 (US-IMP-13 AC1). Posted in one command: the transfer is saved with TWO enlisted ledger entries per line
/// (<c>TransferOut</c> at the source, <c>TransferIn</c> at the destination, one reference). Instant, no in-transit state
/// (OQ-12). Moving an expired or recalled batch is allowed -- BR-3 forbids USE, not movement -- so a transfer can quarantine
/// or consolidate it. Immutable once posted.
/// </summary>
public sealed class StockTransferAggregate : AggregateRoot
{
    private readonly List<StockTransferLine> _lines = [];

    public string Number { get; private set; } = default!;
    public Guid BranchId { get; private set; }
    public Guid FromLocationId { get; private set; }
    public Guid ToLocationId { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset TransferredOn { get; private set; }
    public Guid TransferredBy { get; private set; }
    public IReadOnlyList<StockTransferLine> Lines => _lines;

    private StockTransferAggregate() { }

    public StockTransferAggregate(Guid id) : base(id) { }

    public void Post(StockTransferPostDto dto)
    {
        if (dto.FromLocationId == dto.ToLocationId)
            throw InventoryViolation.Of(BusinessViolationCodes.TransferLocationsMustDiffer, "A transfer needs two different locations.", nameof(ToLocationId));
        if (dto.Lines.Count == 0 || dto.Lines.Count > InventoryRules.MaxTransferLines)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidQuantity, $"A transfer has between 1 and {InventoryRules.MaxTransferLines} lines.", nameof(Lines));
        if (dto.Reason is { Length: > 500 })
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidQuantity, "The reason cannot exceed 500 characters.", nameof(Reason));
        if (dto.Lines.Select(l => l.BatchId).Distinct().Count() != dto.Lines.Count)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidQuantity, "A batch can appear on a transfer only once.", nameof(Lines));

        _lines.Clear();
        for (var i = 0; i < dto.Lines.Count; i++)
        {
            var quantity = dto.Lines[i].Quantity;
            if (quantity <= 0m || decimal.Round(quantity, 3) != quantity)
                throw InventoryViolation.Of(BusinessViolationCodes.InvalidQuantity, "The quantity must be greater than zero, with at most three decimal places.", $"Lines[{i}].Quantity");
            _lines.Add(new StockTransferLine(dto.Lines[i].LineId, dto.Lines[i].SkuId, dto.Lines[i].BatchId, quantity));
        }

        Number = dto.Number;
        BranchId = dto.BranchId;
        FromLocationId = dto.FromLocationId;
        ToLocationId = dto.ToLocationId;
        Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        TransferredOn = dto.TransferredOn;
        TransferredBy = dto.Actor.UserContext.UserId;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new StockTransferPostedEvent
        {
            TransferId = Id, FromLocationId = FromLocationId, ToLocationId = ToLocationId, LineCount = _lines.Count,
            CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext
        });
        UpdateVersion();
    }

    /// <summary>The paired entries: out at the source, in at the destination, same reference (US-IMP-13 AC1). Each end carries the branch of ITS
    /// location (a transfer may cross branches when the caller is assigned to both), so <paramref name="toBranchId"/> is the destination's.</summary>
    public IReadOnlyList<StockMovementDraft> BuildDrafts(Guid toBranchId) =>
        _lines.SelectMany(l => new[]
        {
            new StockMovementDraft(BranchId, FromLocationId, l.SkuId, l.BatchId, MovementKind.TransferOut, -l.Quantity, StockReferenceKind.StockTransfer, Id, l.Id, Number, Reason),
            new StockMovementDraft(toBranchId, ToLocationId, l.SkuId, l.BatchId, MovementKind.TransferIn, l.Quantity, StockReferenceKind.StockTransfer, Id, l.Id, Number, Reason),
        }).ToList();
}
