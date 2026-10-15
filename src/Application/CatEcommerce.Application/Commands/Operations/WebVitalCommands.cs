using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Operations;

/// <summary>One measurement of a beacon. <c>Metric</c> is a <c>WebVitalMetric</c> display name, <c>ViewportClass</c> a <c>ViewportClass</c> display name.</summary>
public sealed record WebVitalSampleInput(
    string Metric, decimal Value, string RouteTemplate, string NavigationType, string? EffectiveConnectionType, string ViewportClass);

/// <summary>
/// HO-XCT-001 section 11.1 C11 -- records one anonymous batch of web-vital samples. The handler replaces the user context with the anonymous one
/// whoever called, so no identity is stored (section 20.8).
/// </summary>
public sealed class RecordWebVitalsCommand : Command
{
    public required Guid BatchId { get; init; }
    public required string BuildId { get; init; }
    public required IReadOnlyList<WebVitalSampleInput> Samples { get; init; }
}
