using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Read.ViewModels;

// HO-EMR-001 section 12.4 (Phase F). Quantities travel as { value, unit } (UI-8): the value a string, the unit the SKU stock unit snapshotted on
// the line ("each" for a procedure, which takes no stock). Prices are the snapshots taken when the line was recorded (ADR-127).

public sealed record VisitTreatmentBatchViewModel(Guid Id, string LotNumber, DateOnly? ExpiresOn);

public sealed record VisitTreatmentViewModel(
    Guid Id, string Kind, string Description, Guid? ServiceId, Guid? SkuId, string? SkuName,
    Measurement Quantity, Measurement DeductedQuantity, Measurement ShortfallQuantity,
    VisitTreatmentBatchViewModel? Batch, string? BatchOverrideReason,
    string? Dose, string? Route, bool IsBillable, MoneyViewModel? UnitPrice,
    string Status, DateTimeOffset RecordedOn, string RecordedBy,
    DateTimeOffset? ReversedOn, string? ReversedBy, string? ReversalReason);

/// <summary><c>GET /api/staff/dispensary/items</c>. <see cref="AvailableQuantity"/> counts only batches Inventory would let a vet use and
/// nothing reserved. A controlled item is listed (so the client can show why it cannot be chosen) but is refused on record.</summary>
public sealed record DispensaryItemViewModel(Guid SkuId, string Name, string StockUnit, string AvailableQuantity, bool IsControlledDrug);

/// <summary><c>GET /api/staff/dispensary/items/{skuId}/batches</c>.</summary>
public sealed record IssuableBatchViewModel(Guid BatchId, string LotNumber, DateOnly? ExpiresOn, string AvailableQuantity, bool IsFefoDefault);

/// <summary>One billable treatment line, as Billing reads it (HO-BIL-001 Phase J). <see cref="ServiceId"/> is set for a procedure billed at a
/// service price, <see cref="SkuId"/> for a medicine or consumable; <see cref="Unit"/> is the stock unit ("each" for a procedure).</summary>
public sealed record VisitChargeLineViewModel(
    Guid LineId, string Kind, string Description, string Quantity, string Unit, MoneyViewModel UnitPrice, Guid TaxClassId, Guid? ServiceId, Guid? SkuId);

/// <summary>The finalised visit's charge lines. <see cref="CustomerId"/> is the appointment's customer (the visit holds none); the lines are the
/// billable, non-reversed ones, oldest first. <see cref="ServiceDate"/> is the branch-local date the visit was finalised (the same rule as the appointment source, which uses the date of <c>CompletedOn</c>).</summary>
public sealed record VisitChargeLinesViewModel(
    Guid VisitId, Guid AppointmentId, Guid CatId, Guid CustomerId, Guid BranchId, DateTimeOffset FinalisedOn, DateOnly ServiceDate,
    IReadOnlyList<VisitChargeLineViewModel> Lines);
