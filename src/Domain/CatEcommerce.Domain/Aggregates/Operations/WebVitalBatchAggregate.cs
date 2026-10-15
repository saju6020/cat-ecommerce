using System.Text.RegularExpressions;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Operations;

/// <summary>
/// HO-XCT-001 section 8.6 / ADR-309. One beacon of anonymous real-user web-vital samples. It raises no event and carries no identity: <c>CreatedBy</c> is set
/// from the anonymous user context by the handler even when the caller is signed in (section 20.8). Excluded from change capture (a row per page view must
/// not write an audit row per page view).
/// </summary>
public sealed partial class WebVitalBatchAggregate : AggregateRoot
{
    public const int MaxBuildIdLength = 64;
    public const int MaxRouteLength = 200;

    private readonly List<WebVitalSample> _samples = [];

    public DateTimeOffset ReceivedOn { get; private set; }
    public string BuildId { get; private set; } = default!;
    public IReadOnlyList<WebVitalSample> Samples => _samples;

    private WebVitalBatchAggregate() { }

    public WebVitalBatchAggregate(Guid id) : base(id) { }

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex BuildIdPattern();

    public void Record(WebVitalBatchDto dto)
    {
        if (_samples.Count > 0) throw Invalid("This batch has already been recorded.");
        if (dto.Samples.Count < 1 || dto.Samples.Count > WebVitalBudgets.MaxSamplesPerBatch) throw Invalid("A batch holds one to ten samples.");
        if (string.IsNullOrEmpty(dto.BuildId) || dto.BuildId.Length > MaxBuildIdLength || !BuildIdPattern().IsMatch(dto.BuildId)) throw Invalid("The build id is not valid.");

        foreach (var sample in dto.Samples)
        {
            var limit = sample.Metric == WebVitalMetric.CLS ? WebVitalBudgets.MaxClsValue : WebVitalBudgets.MaxTimeMetricMs;
            if (sample.Value < 0 || sample.Value > limit) throw Invalid("A sample value is out of range.");
            if (string.IsNullOrEmpty(sample.RouteTemplate) || sample.RouteTemplate.Length > MaxRouteLength) throw Invalid("A route is not valid.");
            if (!WebVitalBudgets.NavigationTypes.Contains(sample.NavigationType)) throw Invalid("A navigation type is not valid.");
            if (sample.EffectiveConnectionType is not null && !WebVitalBudgets.EffectiveConnectionTypes.Contains(sample.EffectiveConnectionType)) throw Invalid("A connection type is not valid.");
        }

        ReceivedOn = dto.ReceivedOn;
        BuildId = dto.BuildId;
        foreach (var sample in dto.Samples)
            _samples.Add(new WebVitalSample(Guid.NewGuid(), sample.Metric, sample.Value, sample.RouteTemplate, sample.NavigationType, sample.EffectiveConnectionType, sample.ViewportClass));

        SetDefaultValue(dto.UserContext);
        UpdateVersion();
    }

    private static DomainRuleViolationException Invalid(string message) =>
        new(BusinessViolationCodes.InvalidWebVitalsPayload, message, nameof(Samples));
}
