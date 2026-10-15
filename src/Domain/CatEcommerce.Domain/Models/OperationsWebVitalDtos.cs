using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-XCT-001 section 8.6. One measurement as received; the user context is always the anonymous one.</summary>
public sealed record WebVitalSampleDto(
    WebVitalMetric Metric, decimal Value, string RouteTemplate, string NavigationType, string? EffectiveConnectionType, ViewportClass ViewportClass);

public sealed record WebVitalBatchDto(DateTimeOffset ReceivedOn, string BuildId, IReadOnlyList<WebVitalSampleDto> Samples, UserContext UserContext);
