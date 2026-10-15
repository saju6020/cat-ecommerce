namespace CatEcommerce.Api.Contracts.Visit;

/// <summary>HO-EMR-001 section 12.4 -- <c>POST /api/staff/visits/{visitId}/treatments</c>. The line id is minted in the controller.</summary>
public sealed class RecordTreatmentRequest
{
    /// <summary>The client's idempotency key (optional). Send the same id on a retry: the lines already recorded are returned (200) and nothing is
    /// deducted twice. The server mints one when omitted, in which case a retry is a new treatment.</summary>
    public Guid? LineId { get; init; }

    /// <summary><c>Procedure</c>, <c>Medicine</c> or <c>Consumable</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Required for a procedure; for a medicine or consumable it defaults to the item name.</summary>
    public string? Description { get; init; }

    /// <summary>A billable clinic service (procedure only).</summary>
    public Guid? ServiceId { get; init; }

    /// <summary>Required for a medicine or consumable: the dispensary item (<c>GET /api/staff/dispensary/items</c>).</summary>
    public Guid? SkuId { get; init; }

    /// <summary>Omitted: the draw starts at the FEFO first batch and continues in FEFO order across batches. Given: that batch is drawn first (and
    /// <see cref="BatchOverrideReason"/> is required when it is not the FEFO first, BR-4), then the draw continues in FEFO order over the others.</summary>
    public Guid? BatchId { get; init; }

    public required decimal Quantity { get; init; }
    public string? Dose { get; init; }
    public string? Route { get; init; }
    public bool IsBillable { get; init; }

    /// <summary>Re-post after <c>400 InsufficientStock</c> to record the treatment anyway and note the shortfall for reordering (AC2).</summary>
    public bool AcceptShortfall { get; init; }
    public string? BatchOverrideReason { get; init; }
}

public sealed class ReverseTreatmentRequest
{
    public required string Reason { get; init; }
}
