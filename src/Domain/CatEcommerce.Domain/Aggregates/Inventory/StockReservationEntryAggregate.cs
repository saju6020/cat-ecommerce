using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

public sealed record StockReservationEntryDto(
    Guid BranchId, Guid LocationId, Guid SkuId, Guid BatchId, ReservationEntryKind Kind, decimal QuantityDelta,
    StockReferenceKind ReferenceKind, Guid ReferenceId, Guid ReferenceLineId, Guid? CustomerId, Guid? CatId,
    bool FefoOverridden, string? OverrideReason, Guid PerformedBy, DateTimeOffset OccurredOn, UserContext UserContext);

/// <summary>
/// HO-IMP-001 §8.7 / ADR-098, ADR-100. A reservation is an ENTRY, not a ledger movement: <c>Reserve</c> (+) and <c>Release</c> (-).
/// Append-only and immutable like <see cref="StockLedgerEntryAggregate"/>. The REMAINING reservation of a key
/// <c>(ReferenceKind, ReferenceId, ReferenceLineId, BatchId, LocationId)</c> is derived: the sum of its reservation entries plus the
/// sum of its consuming ledger entries (negative). Nothing stores it.
/// </summary>
public sealed class StockReservationEntryAggregate : AggregateRoot
{
    public long Sequence { get; private set; }
    public DateTimeOffset OccurredOn { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid BatchId { get; private set; }
    public ReservationEntryKind Kind { get; private set; } = default!;
    public decimal QuantityDelta { get; private set; }
    public StockReferenceKind ReferenceKind { get; private set; } = default!;
    public Guid ReferenceId { get; private set; }
    public Guid ReferenceLineId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public Guid? CatId { get; private set; }
    public bool FefoOverridden { get; private set; }
    public string? OverrideReason { get; private set; }
    public Guid PerformedBy { get; private set; }

    private StockReservationEntryAggregate() { }

    private StockReservationEntryAggregate(Guid id) : base(id) { }

    public static StockReservationEntryAggregate Record(StockReservationEntryDto dto)
    {
        var expectedPositive = dto.Kind == ReservationEntryKind.Reserve;
        if (dto.QuantityDelta == 0m || (dto.QuantityDelta > 0m) != expectedPositive)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidQuantity, $"A {dto.Kind.DisplayName} entry must carry a {(expectedPositive ? "positive" : "negative")} quantity.", nameof(QuantityDelta));

        var entry = new StockReservationEntryAggregate(Guid.NewGuid())
        {
            OccurredOn = dto.OccurredOn, BranchId = dto.BranchId, LocationId = dto.LocationId, SkuId = dto.SkuId, BatchId = dto.BatchId,
            Kind = dto.Kind, QuantityDelta = dto.QuantityDelta, ReferenceKind = dto.ReferenceKind, ReferenceId = dto.ReferenceId,
            ReferenceLineId = dto.ReferenceLineId, CustomerId = dto.CustomerId, CatId = dto.CatId, FefoOverridden = dto.FefoOverridden,
            OverrideReason = dto.OverrideReason, PerformedBy = dto.PerformedBy
        };
        entry.SetDefaultValue(dto.UserContext);
        entry.UpdateVersion();
        return entry;
    }
}
