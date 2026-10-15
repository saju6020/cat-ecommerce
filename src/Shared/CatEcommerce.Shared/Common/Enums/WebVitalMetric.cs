namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-XCT-001 §10.1 -- the five Core Web Vitals / navigation metrics the storefront reports (ADR-309).</summary>
public sealed class WebVitalMetric(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly WebVitalMetric LCP = new(1, nameof(LCP));
    public static readonly WebVitalMetric INP = new(2, nameof(INP));
    public static readonly WebVitalMetric CLS = new(3, nameof(CLS));
    public static readonly WebVitalMetric FCP = new(4, nameof(FCP));
    public static readonly WebVitalMetric TTFB = new(5, nameof(TTFB));
}
