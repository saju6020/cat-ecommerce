using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-EMR-001 §8.6 -- one treatment of a visit: a procedure, a medicine given or a consumable used. Owned by
/// <c>VisitAggregate</c>; it freezes with the visit and is <b>never removed</b> -- a mistake becomes <see cref="TreatmentLineStatus.Reversed"/>
/// (the table is <c>RecordClass.Clinical</c>, no DELETE ever, C-ALL-4).
///
/// A line is <b>one batch</b>: <see cref="BatchId"/>, <see cref="LotNumber"/> and <see cref="BatchExpiresOn"/> are the batch the stock was
/// taken from (A10 "exact batch"), snapshotted so a later batch edit cannot rewrite the record. <b>No stock quantity of the shelf is stored</b>
/// (D-3): <see cref="DeductedQuantity"/> is what this treatment took, and Inventory's ledger is the only stock record.
/// <see cref="Quantity"/> = <see cref="DeductedQuantity"/> + <see cref="ShortfallQuantity"/>: the shortfall is the part the dispensary could not
/// supply (the treatment was given; Inventory was told, so it can reorder).
///
/// <see cref="UnitPrice"/> and <see cref="TaxClassId"/> are the service's or SKU's price and tax class <b>at the moment of recording</b> (ADR-127,
/// HO-ADM-001 ADR-049) -- snapshotted, never re-read; Billing reads them through <c>GetVisitChargeLinesQuery</c>.
/// </summary>
public sealed class TreatmentLine
{
    public Guid Id { get; private set; }
    public TreatmentKind Kind { get; private set; } = default!;
    public string Description { get; private set; } = default!;

    // Named ClinicServiceId, like the visit's own: the platform Entity base declares ServiceId on aggregates.
    public Guid? ClinicServiceId { get; private set; }
    public Guid? SkuId { get; private set; }
    public string? SkuName { get; private set; }
    public string? StockUnit { get; private set; }

    public Guid? BatchId { get; private set; }
    public string? LotNumber { get; private set; }
    public DateOnly? BatchExpiresOn { get; private set; }
    public Guid? LocationId { get; private set; }

    public decimal Quantity { get; private set; }
    public decimal DeductedQuantity { get; private set; }
    public decimal ShortfallQuantity { get; private set; }

    public string? Dose { get; private set; }
    public string? Route { get; private set; }
    public bool IsBillable { get; private set; }

    public decimal? UnitPriceAmount { get; private set; }
    public string? UnitPriceCurrency { get; private set; }
    public Guid? TaxClassId { get; private set; }

    public string? BatchOverrideReason { get; private set; }

    /// <summary>The draw index within one request (0, 1, ...): lines of one request share an instant, and this is the order they were drawn in.</summary>
    public int Position { get; private set; }

    public TreatmentLineStatus Status { get; private set; } = TreatmentLineStatus.Recorded;
    public DateTimeOffset RecordedOn { get; private set; }
    public Guid RecordedBy { get; private set; }
    public DateTimeOffset? ReversedOn { get; private set; }
    public Guid? ReversedBy { get; private set; }
    public string? ReversalReason { get; private set; }

    public Money? UnitPrice => UnitPriceAmount is { } amount && UnitPriceCurrency is { } currency ? new Money(amount, currency) : null;

    public bool IsReversed => Status == TreatmentLineStatus.Reversed;

    private TreatmentLine() { }

    public TreatmentLine(
        Guid id, TreatmentKind kind, string description, Guid? clinicServiceId, Guid? skuId, string? skuName, string? stockUnit,
        Guid? batchId, string? lotNumber, DateOnly? batchExpiresOn, Guid? locationId,
        decimal quantity, decimal deductedQuantity, decimal shortfallQuantity, string? dose, string? route, bool isBillable,
        Money? unitPrice, Guid? taxClassId, string? batchOverrideReason, Guid recordedBy, DateTimeOffset recordedOn, int position = 0)
    {
        Id = id;
        Kind = kind;
        Description = description;
        ClinicServiceId = clinicServiceId;
        SkuId = skuId;
        SkuName = skuName;
        StockUnit = stockUnit;
        BatchId = batchId;
        LotNumber = lotNumber;
        BatchExpiresOn = batchExpiresOn;
        LocationId = locationId;
        Quantity = quantity;
        DeductedQuantity = deductedQuantity;
        ShortfallQuantity = shortfallQuantity;
        Dose = dose;
        Route = route;
        IsBillable = isBillable;
        UnitPriceAmount = unitPrice?.Amount;
        UnitPriceCurrency = unitPrice?.CurrencyCode;
        TaxClassId = taxClassId;
        BatchOverrideReason = batchOverrideReason;
        RecordedBy = recordedBy;
        RecordedOn = recordedOn;
        Position = position;
        Status = TreatmentLineStatus.Recorded;
    }

    public void Reverse(string reason, Guid reversedBy, DateTimeOffset now)
    {
        Status = TreatmentLineStatus.Reversed;
        ReversalReason = reason;
        ReversedBy = reversedBy;
        ReversedOn = now;
    }
}
