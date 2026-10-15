using System.Text.RegularExpressions;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>
/// HO-XCT-001 section 11.2 -- shape only. EVERY failure carries the one generic code <c>InvalidWebVitalsPayload</c> so the public endpoint is not an
/// oracle for which field was wrong.
/// </summary>
public sealed partial class RecordWebVitalsCommandValidator : AbstractValidator<RecordWebVitalsCommand>
{
    [GeneratedRegex(@"^/[a-z0-9\-\[\]\(\)/\.]{0,199}$")]
    private static partial Regex RoutePattern();

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex BuildIdPattern();

    public RecordWebVitalsCommandValidator()
    {
        var code = BusinessViolationCodes.InvalidWebVitalsPayload;

        RuleFor(x => x.BatchId).NotEmpty().WithErrorCode(code);
        RuleFor(x => x.BuildId).Must(b => b is not null && BuildIdPattern().IsMatch(b)).WithErrorCode(code);
        RuleFor(x => x.Samples).Must(s => s is { Count: >= 1 and <= WebVitalBudgets.MaxSamplesPerBatch }).WithErrorCode(code);

        RuleForEach(x => x.Samples).ChildRules(sample =>
        {
            sample.RuleFor(s => s.Metric).Must(m => Enumeration.GetAll<WebVitalMetric>().Any(e => e.DisplayName == m)).WithErrorCode(code);
            sample.RuleFor(s => s.ViewportClass).Must(v => Enumeration.GetAll<ViewportClass>().Any(e => e.DisplayName == v)).WithErrorCode(code);
            sample.RuleFor(s => s.RouteTemplate).Must(r => r is not null && RoutePattern().IsMatch(r)).WithErrorCode(code);
            sample.RuleFor(s => s.NavigationType).Must(n => n is not null && WebVitalBudgets.NavigationTypes.Contains(n)).WithErrorCode(code);
            sample.RuleFor(s => s.EffectiveConnectionType).Must(e => e is null || WebVitalBudgets.EffectiveConnectionTypes.Contains(e)).WithErrorCode(code);
            sample.RuleFor(s => s.Value).GreaterThanOrEqualTo(0).WithErrorCode(code);
        }).When(x => x.Samples is not null);
    }
}
