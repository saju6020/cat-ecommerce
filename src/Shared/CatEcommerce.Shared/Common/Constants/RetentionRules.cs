namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-XCT-001 §10.3 -- fixed numbers of the retention worker. Periods themselves are settings
/// (<c>retention.*</c>, ADR-301), never constants.</summary>
public static class RetentionRules
{
    public const int BatchSize = 500;
    public const int ErasurePassMinutes = 15;
    public const int BackupRunDays = 400;
    public const int ReapplyMaxIds = 500;

    /// <summary>A daily pass drains a purge class in batches of <see cref="BatchSize"/>, but never more than this many per pass (§12.2).</summary>
    public const int MaxBatchesPerPass = 20;

    /// <summary>A run that has been Running longer than this was abandoned by a crashed worker and no longer blocks a new request (C5).</summary>
    public const int StaleRunHours = 6;

    /// <summary>Notifications redacted per daily pass (each is its own command, so the pass is bounded).</summary>
    public const int MaxRedactionsPerPass = 1000;

    /// <summary>Failing erasure scope is retried this many minutes after its Nth failed attempt (attempt 1 -> 15, ..., attempt 4+ -> 1440).</summary>
    public static readonly IReadOnlyList<int> ErasureBackoffMinutes = [15, 60, 240, 1440];

    /// <summary>Minutes to wait after <paramref name="attempts"/> failed attempts (1-based); beyond the table, the last value.</summary>
    public static int BackoffAfter(int attempts) =>
        ErasureBackoffMinutes[Math.Clamp(attempts, 1, ErasureBackoffMinutes.Count) - 1];
}
