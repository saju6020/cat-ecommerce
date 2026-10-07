namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-ACC-001 §10.2 / U-A4. Progressive lockout delay, monotonic non-decreasing and capped --
/// the shape is the requirement (US-ACC-04 AC3).
/// </summary>
public static class LoginThrottleRules
{
    /// <summary>Seconds to lock out after <paramref name="consecutiveFailures"/> failures.</summary>
    public static int DelayFor(int consecutiveFailures) => consecutiveFailures switch
    {
        <= 2 => 0,
        3 => 5,
        4 => 15,
        5 => 60,
        6 => 300,
        _ => 900
    };
}
