namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-XCT-001 §10.3 / ADR-309, ADR-310 -- the NFR-PRF-1 budget and the bounds of the telemetry endpoint.</summary>
public static class WebVitalBudgets
{
    public const int LcpMs = 2500;
    public const int InpMs = 200;
    public const decimal Cls = 0.1m;
    public const int MinimumSamples = 50;
    public const int MaxSamplesPerBatch = 10;
    public const int MaxPayloadBytes = 4096;
    public const int MaxRangeDays = 93;

    /// <summary>Upper bound for a plausible time metric (ms) and for CLS (§8.6).</summary>
    public const decimal MaxTimeMetricMs = 120_000m;
    public const decimal MaxClsValue = 50m;

    public static readonly IReadOnlyList<string> NavigationTypes =
        ["navigate", "reload", "back-forward", "back-forward-cache", "prerender", "restore"];

    public static readonly IReadOnlyList<string> EffectiveConnectionTypes = ["4g", "3g", "2g", "slow-2g"];
}
