using System.Globalization;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// HO-ORD-001 §8.5 (ADR-198) -- money in <c>long</c> minor units (paisa for BDT). Every pricing step in Ordering is integer arithmetic on
/// this type; <c>decimal</c> appears only at the boundary, converted exactly (<c>amount x 100</c>). Rounding is <b>half away from zero</b>
/// and happens at the line, never on a total; shares are apportioned by largest remainder so they sum exactly (US-ORD-04 AC2).
/// No <c>double</c> or <c>float</c> anywhere (U-A1).
/// </summary>
public readonly record struct MinorUnits(long Value) : IComparable<MinorUnits>
{
    public const int Digits = 2;
    private const long Scale = 100;

    public static MinorUnits Zero => new(0);

    /// <summary>Converts a stored <c>decimal(12,2)</c> amount exactly. A value with more than two decimals is refused: it was never
    /// valid money, and silently rounding it would hide a defect.</summary>
    public static MinorUnits FromAmount(decimal amount)
    {
        var scaled = amount * Scale;
        if (scaled != decimal.Truncate(scaled))
            throw new ArgumentException($"{amount.ToString(CultureInfo.InvariantCulture)} has more than {Digits} decimal places.", nameof(amount));

        return new MinorUnits(checked((long)scaled));
    }

    /// <summary>The exact <c>decimal</c> amount (two decimals).</summary>
    public decimal ToAmount() => Value / (decimal)Scale;

    public static MinorUnits operator +(MinorUnits left, MinorUnits right) => new(checked(left.Value + right.Value));
    public static MinorUnits operator -(MinorUnits left, MinorUnits right) => new(checked(left.Value - right.Value));
    public static MinorUnits operator -(MinorUnits value) => new(-value.Value);
    public static bool operator <(MinorUnits left, MinorUnits right) => left.Value < right.Value;
    public static bool operator >(MinorUnits left, MinorUnits right) => left.Value > right.Value;
    public static bool operator <=(MinorUnits left, MinorUnits right) => left.Value <= right.Value;
    public static bool operator >=(MinorUnits left, MinorUnits right) => left.Value >= right.Value;

    public int CompareTo(MinorUnits other) => Value.CompareTo(other.Value);

    public static MinorUnits Min(MinorUnits left, MinorUnits right) => left <= right ? left : right;
    public static MinorUnits Max(MinorUnits left, MinorUnits right) => left >= right ? left : right;

    /// <summary>This amount times a whole quantity.</summary>
    public MinorUnits Times(int quantity) => new(checked(Value * quantity));

    /// <summary><c>round(this x percent / 100)</c>, half away from zero (U-A2), in integer arithmetic. <paramref name="percentHundredths"/>
    /// is the percentage in hundredths of a percent (15.00 % is 1500), so a two-decimal percentage is exact.</summary>
    public MinorUnits Percent(long percentHundredths) => new(DivideRoundHalfAwayFromZero(checked(Value * percentHundredths), 10_000));

    /// <summary>The percentage as hundredths of a percent, exactly (a rate with more than two decimals is refused).</summary>
    public static long PercentToHundredths(decimal percent)
    {
        var scaled = percent * 100m;
        if (scaled != decimal.Truncate(scaled))
            throw new ArgumentException($"{percent.ToString(CultureInfo.InvariantCulture)} has more than two decimal places.", nameof(percent));

        return checked((long)scaled);
    }

    /// <summary>Integer division of <paramref name="numerator"/> by <paramref name="denominator"/>, rounded half away from zero.</summary>
    public static long DivideRoundHalfAwayFromZero(long numerator, long denominator)
    {
        if (denominator == 0) throw new DivideByZeroException();

        var negative = (numerator < 0) != (denominator < 0);
        var n = Math.Abs(numerator);
        var d = Math.Abs(denominator);
        var quotient = n / d;
        var remainder = n % d;
        if (remainder * 2 >= d) quotient++;
        return negative ? -quotient : quotient;
    }

    /// <summary>
    /// Splits <paramref name="total"/> over <paramref name="weights"/> proportionally, by largest remainder, so the shares sum to
    /// <paramref name="total"/> exactly (U-A3: 100 over three equal weights is 34/33/33; 1 over three is 1/0/0). Ties go to the earlier
    /// index, so the result is deterministic. All-zero weights give the whole total to the first element.
    /// </summary>
    public static IReadOnlyList<MinorUnits> Apportion(MinorUnits total, IReadOnlyList<MinorUnits> weights)
    {
        if (weights.Count == 0) return [];
        if (total.Value < 0) throw new ArgumentOutOfRangeException(nameof(total), "Only a non-negative amount is apportioned.");

        var weightSum = weights.Sum(w => w.Value);
        if (weightSum <= 0)
        {
            var allToFirst = new MinorUnits[weights.Count];
            allToFirst[0] = total;
            return allToFirst;
        }

        var shares = new long[weights.Count];
        var remainders = new long[weights.Count];
        long assigned = 0;
        for (var i = 0; i < weights.Count; i++)
        {
            var product = checked((Int128)total.Value * weights[i].Value);
            shares[i] = (long)(product / weightSum);
            remainders[i] = (long)(product % weightSum);
            assigned += shares[i];
        }

        var leftover = total.Value - assigned;
        foreach (var index in Enumerable.Range(0, weights.Count).OrderByDescending(i => remainders[i]).ThenBy(i => i).Take((int)leftover))
            shares[index]++;

        return shares.Select(s => new MinorUnits(s)).ToList();
    }

    public override string ToString() => ToAmount().ToString("0.00", CultureInfo.InvariantCulture);
}
