using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-BRD-001 section 8.8 -- one line of the snapshotted quote, in chronological order. Owned by <see cref="Aggregates.Boarding.BoardingStayAggregate"/>;
/// replaced wholesale when the quote is recalculated (extension, early check-out), so it is current state rather than a record. <see cref="Amount"/>
/// is the gross amount of the unit; <see cref="Discount"/> is the long-stay discount carried by THIS unit (per unit, so the sum of per-cycle invoices
/// equals the quote to the paisa -- U-B8).
/// </summary>
public sealed class QuotedRateUnit
{
    public Guid Id { get; private set; }
    public int Sequence { get; private set; }
    public RateUnitKind Kind { get; private set; } = default!;

    /// <summary>1-based night numbers the unit covers, inclusive.</summary>
    public int FromNight { get; private set; }
    public int ToNight { get; private set; }

    public decimal Amount { get; private set; }
    public decimal Discount { get; private set; }
    public string Description { get; private set; } = default!;

    public int Nights => ToNight - FromNight + 1;
    public decimal Net => Amount - Discount;

    private QuotedRateUnit() { }

    public QuotedRateUnit(Guid id, int sequence, RateUnitKind kind, int fromNight, int toNight, decimal amount, decimal discount, string description)
    {
        Id = id;
        Sequence = sequence;
        Kind = kind;
        FromNight = fromNight;
        ToNight = toNight;
        Amount = amount;
        Discount = discount;
        Description = description;
    }
}
