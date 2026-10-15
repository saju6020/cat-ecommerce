using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>The outcome of <see cref="VitalRangeCheck.Evaluate"/>: signs outside their <i>absolute</i>
/// range (refused, no confirmation can save them) and signs outside their <i>plausible</i> range
/// (refused until confirmed).</summary>
public sealed record VitalRangeResult(IReadOnlyList<VitalSign> AbsoluteViolations, IReadOnlyList<VitalSign> PlausibleWarnings)
{
    public bool IsClean => AbsoluteViolations.Count == 0 && PlausibleWarnings.Count == 0;
}

/// <summary>
/// HO-EMR-001 §8.3 / ADR-120 -- pure, two-tier range check. Bounds are inclusive: a value exactly on a
/// plausible bound raises no warning (U-B7). A value outside the absolute range is reported as an
/// absolute violation <i>only</i> (not also as a plausible warning), so the caller refuses it
/// outright.
/// </summary>
public static class VitalRangeCheck
{
    public static VitalRangeResult Evaluate(IReadOnlyDictionary<VitalSign, decimal> values)
    {
        var absolute = new List<VitalSign>();
        var plausible = new List<VitalSign>();

        foreach (var (sign, value) in values)
        {
            var range = VitalSignRules.For(sign);

            if (value < range.AbsoluteMin || value > range.AbsoluteMax)
            {
                absolute.Add(sign);
                continue;
            }

            if (range.PlausibleMin is { } min && range.PlausibleMax is { } max && (value < min || value > max))
                plausible.Add(sign);
        }

        return new VitalRangeResult(absolute, plausible);
    }
}
