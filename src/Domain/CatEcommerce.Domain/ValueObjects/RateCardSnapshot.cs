using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-BRD-001 section 8.3/8.8 (ADR-221, renumbered ADR-354): the agreed prices, taken at reserve from the rate card in force and reused for
/// extension and early check-out (HO-ADM-001 ADR-049: snapshot at transaction time). Plain decimals in <see cref="CurrencyCode"/>; the owner of the
/// numbers is the rate card.
/// </summary>
public sealed record RateCardSnapshot
{
    public Guid CardId { get; init; }
    public BoardingUnitType UnitType { get; init; } = default!;
    public decimal NightlyRate { get; init; }
    public decimal WeeklyRate { get; init; }
    public decimal MonthlyRate { get; init; }
    public decimal AdditionalCatNightlyRate { get; init; }
    public Guid TaxClassId { get; init; }
    public string CurrencyCode { get; init; } = default!;
}

/// <summary>The long-stay and early-check-out settings in force at reserve, snapshotted so a later settings change does not re-price a stay.</summary>
public sealed record PricingSnapshot
{
    public int LongStayThresholdNights { get; init; }
    public decimal LongStayDiscountPercent { get; init; }
    public int EarlyCheckoutChargedNights { get; init; }
    public bool EarlyCheckoutRefundAsCredit { get; init; }
}
