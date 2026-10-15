using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using CatEcommerce.Api.RateLimiting;
using Xunit;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>HO-RPT-001 TEST gate (2026-10-13), T-U5 and T-U6. No host, no database.</summary>
public sealed class ReportingTestGateUnitTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CatEcommerce.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The repository root (CatEcommerce.sln) was not found.");
    }

    // ── T-U5: the guard-matrix test cannot see a deleted CALL to the guard, so look for the calls ────────────────────
    [Fact]
    public void T_U5_TheFactoryConstructor_AndTheScheduleRetirement_BothCallTheTestDatabaseGuard()
    {
        var tests = Path.Combine(RepositoryRoot(), "tests", "CatEcommerce.IntegrationTests");

        var factory = File.ReadAllText(Path.Combine(tests, "ReportingApiFactory.cs"));
        var constructor = Regex.Match(factory, @"public ReportingApiFactory\(\)\s*\{(?<body>[^}]*)\}");
        Assert.True(constructor.Success, "ReportingApiFactory has no parameterless constructor.");
        Assert.Contains("ReportingTestDatabase.Require()", constructor.Groups["body"].Value);

        var schedules = File.ReadAllText(Path.Combine(tests, "Endpoints", "ReportingScheduleEndpointTests.cs"));
        var retire = Regex.Match(schedules, @"protected Task RetireOtherSchedulesAsync\(\)\s*\{(?<body>[^}]*)\}");
        Assert.True(retire.Success, "RetireOtherSchedulesAsync was not found.");
        Assert.Contains("ReportingTestDatabase.Require()", retire.Groups["body"].Value);

        // The guard runs BEFORE the statement that cancels every schedule.
        var body = retire.Groups["body"].Value;
        Assert.True(body.IndexOf("ReportingTestDatabase.Require()", StringComparison.Ordinal) < body.IndexOf("RetireAsync()", StringComparison.Ordinal));
    }

    // ── T-U6: a rejection in a later limiter gives the earlier leases back ────────────────────────────────────────────
    [Fact]
    public void T_U6_ARejectionInTheSecondLimiter_ReleasesTheFirstLease_AndTheNextAttemptAfterTheReleaseIsAcquired()
    {
        using var first = new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = 2, QueueLimit = 0 });
        using var second = new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 });
        using var chain = new ChainedLimiter([first], [second], TimeSpan.FromSeconds(5));

        var held = chain.AttemptAcquire();
        Assert.True(held.IsAcquired);

        // The second limiter is full: the chain refuses, with a retry hint, and the first limiter's permit taken for this attempt is returned.
        using (var refused = chain.AttemptAcquire())
        {
            Assert.False(refused.IsAcquired);
            Assert.True(refused.TryGetMetadata(MetadataName.RetryAfter.Name, out var retry));
            Assert.Equal(TimeSpan.FromSeconds(5), retry);
        }

        Assert.Equal(1, first.GetStatistics()!.CurrentAvailablePermits);   // 2 permits, one held by `held`, none leaked by the refusal

        held.Dispose();
        Assert.Equal(2, first.GetStatistics()!.CurrentAvailablePermits);
        Assert.Equal(1, second.GetStatistics()!.CurrentAvailablePermits);

        using var again = chain.AttemptAcquire();
        Assert.True(again.IsAcquired);
    }

    [Fact]
    public void T_U6_TheChainOwnsItsFirstLimitersOnly_TheSharedHostWideLimiterSurvivesDisposal()
    {
        using var shared = new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 });
        var chain = new ChainedLimiter([new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 })], [shared], TimeSpan.FromSeconds(5));

        chain.Dispose();

        using var lease = shared.AttemptAcquire();       // would throw ObjectDisposedException if the chain had disposed the shared limiter
        Assert.True(lease.IsAcquired);
    }
}
