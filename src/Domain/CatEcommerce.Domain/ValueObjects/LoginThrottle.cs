using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>HO-ACC-001 §8.2. Progressive lockout -- the delay shape lives in <see cref="LoginThrottleRules"/>.</summary>
public sealed record LoginThrottle
{
    public int FailedCount { get; }
    public DateTimeOffset? LockedUntil { get; }
    public DateTimeOffset? LastFailedOn { get; }

    private LoginThrottle(int failedCount, DateTimeOffset? lockedUntil, DateTimeOffset? lastFailedOn)
    {
        FailedCount = failedCount;
        LockedUntil = lockedUntil;
        LastFailedOn = lastFailedOn;
    }

    public static LoginThrottle None { get; } = new(0, null, null);

    public LoginThrottle AfterFailure(DateTimeOffset asOf)
    {
        var newCount = FailedCount + 1;
        var delaySeconds = LoginThrottleRules.DelayFor(newCount);
        var lockedUntil = delaySeconds > 0 ? asOf.AddSeconds(delaySeconds) : (DateTimeOffset?)null;

        return new LoginThrottle(newCount, lockedUntil, asOf);
    }

    public bool IsLocked(DateTimeOffset asOf) => LockedUntil is not null && asOf < LockedUntil;
}
