using System.Threading.RateLimiting;

namespace CatEcommerce.Api.RateLimiting;

/// <summary>
/// HO-RPT-001 section 12.3 (ADR-290) -- Reporting's two policy names, in its own file like every other area (ADR-341; <c>FileRateLimitPolicies</c>, <c>NotificationRateLimitPolicies</c>).
/// <see cref="Reports"/> is a <b>concurrency</b> limiter: one permit per view or export request, <c>MaxConcurrentReports</c> in flight, a small queue, <c>429</c> beyond that. Under a single host
/// (ADR-010) this bounded concurrency is what stands in for NFR-PRF-7's "separate workers". <see cref="ReportRunNow"/> is a fixed window of 5 per 10 minutes per ACCOUNT plus one run in flight per account, then the host-wide limit; <see cref="Dashboard"/> is two in flight per account. Per-account limits are the owner decision of 2026-10-13: one user cannot fill the host-wide limit.
/// </summary>
public static class ReportRateLimitPolicies
{
    public const string Reports = "reports";
    public const string ReportRunNow = "report-run-now";
    public const string Dashboard = "report-dashboard";
}

/// <summary>
/// A <see cref="ConcurrencyLimiter"/> whose rejected lease carries <c>Retry-After: 5</c> (a plain concurrency lease has no retry hint, and the host's shared 429 handler would then answer 60 seconds).
/// A report finishes in seconds, so five is the honest hint.
/// </summary>
public sealed class RetryAfterConcurrencyLimiter(ConcurrencyLimiterOptions options, TimeSpan retryAfter) : RateLimiter
{
    private readonly ConcurrencyLimiter _inner = new(options);

    public override TimeSpan? IdleDuration => _inner.IdleDuration;

    public override RateLimiterStatistics? GetStatistics() => _inner.GetStatistics();

    protected override RateLimitLease AttemptAcquireCore(int permitCount) => Wrap(_inner.AttemptAcquire(permitCount));

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
        Wrap(await _inner.AcquireAsync(permitCount, cancellationToken));

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }

    private RateLimitLease Wrap(RateLimitLease lease) => lease.IsAcquired ? lease : new RejectedLease(retryAfter);

    private sealed class RejectedLease(TimeSpan retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames => [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = retryAfter;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}

/// <summary>
/// HO-RPT-001 gate fix (owner decision 2026-10-13: "add a per-user limit") -- takes one permit from each of several limiters in order, and gives them all back together. The first limiters are
/// per account (so one user cannot fill the host-wide limit and lock everyone else out); the last is the shared host-wide concurrency limiter (ADR-290), which is NOT owned and is never disposed
/// here. A rejection anywhere releases what was already taken and carries <c>Retry-After</c>.
/// </summary>
public sealed class ChainedLimiter(IReadOnlyList<RateLimiter> ownedFirst, IReadOnlyList<RateLimiter> shared, TimeSpan retryAfter, IReadOnlyList<RateLimiter>? ownedLast = null) : RateLimiter
{
    // Order matters: a permit that cannot be given back (a fixed window's) must be taken LAST, after every limiter that can refuse, so a refused attempt never uses one up.
    private readonly IReadOnlyList<RateLimiter> owned = [.. ownedFirst, .. ownedLast ?? []];
    private readonly RateLimiter[] _all = [.. ownedFirst, .. shared, .. ownedLast ?? []];

    // A null idle duration means "in use" (a fixed window still holding used permits, a concurrency limiter with a lease out). The partition may be disposed -- and its window reset -- only when EVERY owned
    // limiter is idle, so one that is null makes the whole chain null.
    public override TimeSpan? IdleDuration => owned.Count == 0 || owned.Any(l => l.IdleDuration is null) ? null : owned.Min(l => l.IdleDuration!.Value);

    public override RateLimiterStatistics? GetStatistics() => _all[0].GetStatistics();

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        var taken = new List<RateLimitLease>();
        foreach (var limiter in _all)
        {
            var lease = limiter.AttemptAcquire(permitCount);
            if (!lease.IsAcquired) return Reject(taken);
            taken.Add(lease);
        }

        return new ChainedLease(taken);
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        var taken = new List<RateLimitLease>();
        foreach (var limiter in _all)
        {
            var lease = await limiter.AcquireAsync(permitCount, cancellationToken);
            if (!lease.IsAcquired) return Reject(taken);
            taken.Add(lease);
        }

        return new ChainedLease(taken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            foreach (var limiter in owned) limiter.Dispose();
        base.Dispose(disposing);
    }

    private RateLimitLease Reject(List<RateLimitLease> taken)
    {
        foreach (var lease in taken) lease.Dispose();
        return new RejectedRetryLease(retryAfter);
    }

    private sealed class ChainedLease(List<RateLimitLease> leases) : RateLimitLease
    {
        public override bool IsAcquired => true;

        public override IEnumerable<string> MetadataNames => [];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (var lease in leases) lease.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class RejectedRetryLease(TimeSpan retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames => [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = retryAfter;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
