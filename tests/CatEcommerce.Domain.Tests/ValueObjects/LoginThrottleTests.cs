using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Domain.Tests.ValueObjects;

/// <summary>HO-ACC-001 §15, U-C1.</summary>
public sealed class LoginThrottleTests
{
    [Fact]
    public void AfterFailure_ThreeTimes_LocksForFiveSeconds()
    {
        var asOf = DateTimeOffset.UtcNow;
        var throttle = LoginThrottle.None
            .AfterFailure(asOf)
            .AfterFailure(asOf)
            .AfterFailure(asOf);

        Assert.Equal(3, throttle.FailedCount);
        Assert.Equal(asOf.AddSeconds(5), throttle.LockedUntil);
    }

    [Fact]
    public void IsLocked_BeforeLockExpiry_ReturnsTrue()
    {
        var asOf = DateTimeOffset.UtcNow;
        var throttle = LoginThrottle.None.AfterFailure(asOf).AfterFailure(asOf).AfterFailure(asOf);

        Assert.True(throttle.IsLocked(asOf.AddSeconds(1)));
        Assert.False(throttle.IsLocked(asOf.AddSeconds(6)));
    }
}
