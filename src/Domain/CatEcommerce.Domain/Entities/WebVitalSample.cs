using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-XCT-001 section 8.6. One anonymous real-user measurement. Owned by <c>WebVitalBatchAggregate</c>. Identifies no one: no user, session, IP or user agent.</summary>
public sealed class WebVitalSample
{
    public Guid Id { get; private set; }
    public WebVitalMetric Metric { get; private set; } = default!;
    public decimal Value { get; private set; }
    public string RouteTemplate { get; private set; } = default!;
    public string NavigationType { get; private set; } = default!;
    public string? EffectiveConnectionType { get; private set; }
    public ViewportClass ViewportClass { get; private set; } = default!;

    private WebVitalSample() { }

    public WebVitalSample(Guid id, WebVitalMetric metric, decimal value, string routeTemplate, string navigationType, string? effectiveConnectionType, ViewportClass viewportClass)
    {
        Id = id;
        Metric = metric;
        Value = value;
        RouteTemplate = routeTemplate;
        NavigationType = navigationType;
        EffectiveConnectionType = effectiveConnectionType;
        ViewportClass = viewportClass;
    }
}
