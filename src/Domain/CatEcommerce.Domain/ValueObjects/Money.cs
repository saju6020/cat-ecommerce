namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// Money is computed in <c>long</c> minor units and stored as <c>decimal(12,2)</c> with an explicit
/// currency code (ADR-317, resolves README C-8 -- the three sources that used to disagree: SRS
/// constraint C-6, ADR-054, and <c>.ai/persistence-ef-core.md</c>'s generic default). This is the
/// one <c>Money</c> type for the whole solution: HO-ADM-001 §8.6 ("<c>Money</c>: <c>decimal(12,2)</c>
/// >= 0 + <c>CurrencyCode</c>"), HO-BIL-001 §8.3, HO-BRD-001 §8.15 and every other handover that
/// prices something all assume exactly this shape without redefining it. No handover names a file
/// for it -- created here because it is a dependency of several of the "at minimum" contracts in
/// docs/handover/README.md's Step 1, before any of their owners is built.
///
/// Business invariants that depend on context (non-negative where a given field requires it,
/// currency must equal <c>commerce.currencyCode</c>) are each owning aggregate's job, not this
/// type's -- e.g. HO-BIL-001 §8.3's <c>ChargeAggregate.Record(ChargeRequestDto)</c> checks
/// <c>CurrencyMismatch</c> itself. This type only guarantees the representation: two decimal
/// places, an upper-case three-letter currency code, and a lossless minor-units round trip.
/// </summary>
public sealed record Money
{
    public decimal Amount { get; }
    public string CurrencyCode { get; }

    public Money(decimal amount, string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
            throw new ArgumentException("CurrencyCode must be a 3-letter ISO-4217 code.", nameof(currencyCode));

        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        CurrencyCode = currencyCode.ToUpperInvariant();
    }

    /// <summary>ADR-317's computed representation -- banker's-rounded minor units (e.g. paisa).</summary>
    public long MinorUnits => (long)decimal.Round(Amount * 100m, 0, MidpointRounding.ToEven);

    public static Money FromMinorUnits(long minorUnits, string currencyCode) =>
        new(minorUnits / 100m, currencyCode);

    public static Money Zero(string currencyCode) => new(0m, currencyCode);

    public static Money operator +(Money left, Money right) => Combine(left, right, (a, b) => a + b);
    public static Money operator -(Money left, Money right) => Combine(left, right, (a, b) => a - b);

    private static Money Combine(Money left, Money right, Func<decimal, decimal, decimal> op)
    {
        if (left.CurrencyCode != right.CurrencyCode)
            throw new ArgumentException(
                $"Cannot combine {left.CurrencyCode} with {right.CurrencyCode}.", nameof(right));

        return new Money(op(left.Amount, right.Amount), left.CurrencyCode);
    }
}
