namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-NTF-001 §8.6/§10.3 -- 1/5/15/60/240 minute backoff, 6 attempts total.</summary>
public static class NotificationRetryRules
{
    public const int MaxAttempts = 6;

    public static readonly IReadOnlyList<TimeSpan> Backoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(60),
        TimeSpan.FromMinutes(240),
    ];

    /// <summary>The delay before the next attempt after <paramref name="attemptsSoFar"/> attempts
    /// have already been made (1-based). Returns <see cref="TimeSpan.Zero"/> once the backoff
    /// table is exhausted -- the caller has already checked <see cref="MaxAttempts"/> by then.</summary>
    public static TimeSpan BackoffFor(int attemptsSoFar) =>
        attemptsSoFar >= 1 && attemptsSoFar <= Backoff.Count ? Backoff[attemptsSoFar - 1] : TimeSpan.Zero;
}
