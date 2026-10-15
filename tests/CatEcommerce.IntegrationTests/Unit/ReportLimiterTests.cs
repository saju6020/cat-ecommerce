using System.Threading.RateLimiting;
using CatEcommerce.Api.RateLimiting;
using Xunit;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>
/// HO-RPT-001 gate re-check row 53 -- <see cref="ChainedLimiter"/> inside a <see cref="PartitionedRateLimiter"/>, which disposes (and so resets) a partition idle for more than 10 seconds. The run-now chain is
/// a fixed window (5 per 10 minutes) plus a one-in-flight limiter; the window still holds used permits when the in-flight limiter goes idle, so the chain must NOT report idle. No host, no database.
/// </summary>
public sealed class ReportLimiterTests
{
    private static PartitionedRateLimiter<string> RunNowLikeChain() =>
        PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.Get(key, _ => new ChainedLimiter(
            [new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 })],
            [], TimeSpan.FromSeconds(5),
            ownedLast: [new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 })])));

    [Fact]
    public async Task TheFifthRunNowOfTheWindow_IsTheLast_EvenAfterTheAccountWasIdleLongerThanTenSeconds()
    {
        using var limiter = RunNowLikeChain();

        for (var run = 1; run <= 5; run++)
        {
            using var lease = limiter.AttemptAcquire("acct:a");
            Assert.True(lease.IsAcquired, $"run {run} should be allowed");
        } // each run has ended: nothing is in flight, but the window has used all five permits

        // A partition idle for more than 10 seconds is disposed by the partitioned limiter's heartbeat; the chain must not look idle while its window holds used permits.
        await Task.Delay(TimeSpan.FromSeconds(23));

        using var sixth = limiter.AttemptAcquire("acct:a");
        Assert.False(sixth.IsAcquired, "the sixth run in the window must be refused");
        Assert.True(sixth.TryGetMetadata(MetadataName.RetryAfter.Name, out _));

        using var other = limiter.AttemptAcquire("acct:b");
        Assert.True(other.IsAcquired, "another account is not affected");
    }

    [Fact]
    public void AChainWithALimiterInUse_IsNotIdle_AndAnIdleChainReportsTheShortestIdleTime()
    {
        using var chain = new ChainedLimiter([new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 })], [], TimeSpan.FromSeconds(5));

        using (chain.AttemptAcquire()) Assert.Null(chain.IdleDuration);   // a lease is out: in use

        Assert.NotNull(chain.IdleDuration);                               // released: idle
    }

    [Fact]
    public void ARefusedAttempt_DoesNotUseUpAPermitOfTheWindow()
    {
        using var limiter = RunNowLikeChain();

        var first = limiter.AttemptAcquire("acct:a");                    // the first run is in flight
        Assert.True(first.IsAcquired);
        using (var doubleClick = limiter.AttemptAcquire("acct:a"))       // refused by the one-in-flight limiter
            Assert.False(doubleClick.IsAcquired);
        first.Dispose();

        // One run and one refused double-click: four more runs remain in the window, and then the window is spent.
        for (var run = 2; run <= 5; run++)
        {
            using var lease = limiter.AttemptAcquire("acct:a");
            Assert.True(lease.IsAcquired, $"run {run} should be allowed");
        }

        using var sixth = limiter.AttemptAcquire("acct:a");
        Assert.False(sixth.IsAcquired);
    }
}
