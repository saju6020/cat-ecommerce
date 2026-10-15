namespace CatEcommerce.Api.Contracts.Operations;

/// <summary>HO-XCT-001 section 12.4 -- the beacon body of <c>POST /api/telemetry/web-vitals</c>.</summary>
public sealed class WebVitalsRequest
{
    public string? BuildId { get; init; }
    public List<WebVitalSampleRequest>? Samples { get; init; }
}

/// <summary><c>Metric</c>: LCP, INP, CLS, FCP or TTFB. <c>ViewportClass</c>: Narrow, Medium or Wide. Value is milliseconds (CLS is unitless).</summary>
public sealed class WebVitalSampleRequest
{
    public string? Metric { get; init; }
    public decimal Value { get; init; }
    public string? RouteTemplate { get; init; }
    public string? NavigationType { get; init; }
    public string? EffectiveConnectionType { get; init; }
    public string? ViewportClass { get; init; }
}
