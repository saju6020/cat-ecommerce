using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Billing;

/// <summary>HO-BIL-001 §11.1 C2 -- a desk charge. Either a catalogue <see cref="ServiceId"/> (its price and tax
/// class are read <b>at now</b>, HO-ADM-001 ADR-049) or a free-text fee (<see cref="Description"/>,
/// <see cref="UnitPrice"/>, <see cref="TaxClassId"/>); never both. The branch is the caller's active branch.</summary>
public sealed class RecordManualChargeCommand : Command
{
    public required Guid ChargeId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid BranchId { get; init; }
    public Guid? CatId { get; init; }
    public Guid? ServiceId { get; init; }
    public string? Description { get; init; }
    public decimal? UnitPrice { get; init; }
    public Guid? TaxClassId { get; init; }
    public required decimal Quantity { get; init; }
    public decimal LineDiscount { get; init; }
    public required DateOnly ServiceDate { get; init; }

    /// <summary>"Clinic" (default), "Shop" or "Boarding" -- a manual charge is the one source that may choose (§8.2).</summary>
    public string? BusinessLine { get; init; }
}

/// <summary>C3 -- void an <b>unbilled</b> charge. A charge on an invoice is corrected by credit note instead.</summary>
public sealed class VoidChargeCommand : Command
{
    public required Guid ChargeId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}

/// <summary>C4 -- capture the billable items of one source (an appointment; from Phase J a visit, dispense or
/// surgery) as charges. Idempotent by source key. <see cref="BranchId"/> is set on the staff path (the source must
/// belong to the active branch) and null on the event path.</summary>
public sealed class CaptureSourceChargesCommand : Command
{
    public required string SourceKind { get; init; }
    public required Guid SourceId { get; init; }
    public Guid? BranchId { get; init; }
}
