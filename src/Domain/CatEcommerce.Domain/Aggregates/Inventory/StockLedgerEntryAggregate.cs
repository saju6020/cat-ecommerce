using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>What <see cref="StockLedgerEntryAggregate.Record"/> needs; built only by <c>EfStockLedger</c> (§8.8).</summary>
public sealed record StockLedgerEntryDto(
    Guid BranchId, Guid LocationId, Guid SkuId, Guid BatchId, MovementKind Kind, decimal QuantityDelta, bool ConsumesReservation,
    StockReferenceKind ReferenceKind, Guid ReferenceId, Guid? ReferenceLineId, string? ReferenceLabel, Guid? CustomerId, Guid? CatId,
    bool FefoOverridden, string? OverrideReason, Guid PerformedBy, string? Note, DateTimeOffset OccurredOn, UserContext UserContext);

/// <summary>
/// HO-IMP-001 §8.7 / ADR-098 -- one row of the stock ledger. <b>Append-only and immutable</b>: no setter, no mutation method, no
/// domain event; the only way to make one is <see cref="Record"/>, and the database refuses UPDATE, DELETE and TRUNCATE
/// (<c>tg_stock_ledger_immutable</c>). Excluded from change capture -- each row already records who, what, when and why. A
/// correction is a NEW entry. It is enlisted in the scoped DbContext by <c>EfStockLedger</c>, never saved through a repository.
/// The sign is fixed by the kind (<c>ck_stock_ledger_sign</c>). <see cref="Sequence"/> is database-generated.
/// </summary>
public sealed class StockLedgerEntryAggregate : AggregateRoot
{
    public long Sequence { get; private set; }
    public DateTimeOffset OccurredOn { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid BatchId { get; private set; }
    public MovementKind Kind { get; private set; } = default!;
    public decimal QuantityDelta { get; private set; }
    public bool ConsumesReservation { get; private set; }
    public StockReferenceKind ReferenceKind { get; private set; } = default!;
    public Guid ReferenceId { get; private set; }
    public Guid? ReferenceLineId { get; private set; }
    public string? ReferenceLabel { get; private set; }
    public Guid? CustomerId { get; private set; }
    public Guid? CatId { get; private set; }
    public bool FefoOverridden { get; private set; }
    public string? OverrideReason { get; private set; }
    public Guid PerformedBy { get; private set; }
    public string? Note { get; private set; }

    private StockLedgerEntryAggregate() { }

    private StockLedgerEntryAggregate(Guid id) : base(id) { }

    public static StockLedgerEntryAggregate Record(StockLedgerEntryDto dto)
    {
        if (!MovementKindRules.IsValidDelta(dto.Kind, dto.QuantityDelta))
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidQuantity, $"A {dto.Kind.DisplayName} entry must carry a {(MovementKindRules.Sign(dto.Kind) > 0 ? "positive" : "negative")} quantity.", nameof(QuantityDelta));
        if (dto.ConsumesReservation && dto.Kind != MovementKind.Sale)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidQuantity, "Only a Sale can consume a reservation.", nameof(ConsumesReservation));

        var entry = new StockLedgerEntryAggregate(Guid.NewGuid())
        {
            OccurredOn = dto.OccurredOn, BranchId = dto.BranchId, LocationId = dto.LocationId, SkuId = dto.SkuId, BatchId = dto.BatchId,
            Kind = dto.Kind, QuantityDelta = dto.QuantityDelta, ConsumesReservation = dto.ConsumesReservation,
            ReferenceKind = dto.ReferenceKind, ReferenceId = dto.ReferenceId, ReferenceLineId = dto.ReferenceLineId, ReferenceLabel = dto.ReferenceLabel,
            CustomerId = dto.CustomerId, CatId = dto.CatId, FefoOverridden = dto.FefoOverridden, OverrideReason = dto.OverrideReason,
            PerformedBy = dto.PerformedBy, Note = dto.Note
        };
        entry.SetDefaultValue(dto.UserContext);
        entry.UpdateVersion();
        return entry;
    }
}
