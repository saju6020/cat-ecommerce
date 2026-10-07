namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// A daily local time-of-day window, used by <c>IBusinessSettings.GetTimeRange</c> (HO-ADM-001
/// §8.9) -- e.g. <c>notifications.quietHours</c>, 21:00-08:00 (HO-NTF-001 §8.14). No handover gives
/// this type's exact member names; inferred from the "any" bounds and the 21:00-08:00 example, both
/// of which need a range that can cross midnight. Flagged as inferred in docs/handover/CONTRACTS.md.
/// </summary>
public sealed record TimeRange(TimeOnly Start, TimeOnly End)
{
    /// <summary>True when <paramref name="instant"/> falls inside the range, wrapping midnight when <see cref="Start"/> &gt; <see cref="End"/>.</summary>
    public bool Contains(TimeOnly instant) =>
        Start <= End
            ? instant >= Start && instant < End
            : instant >= Start || instant < End;
}
