using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-EMR-001 §8.5 / ADR-124 -- pure classification of a result value against the range stored on
/// the value. Numeric: below the low bound is <see cref="ResultFlag.Low"/>, above the high bound
/// <see cref="ResultFlag.High"/>, otherwise <see cref="ResultFlag.Normal"/>; a value with <i>no</i>
/// bound is <see cref="ResultFlag.NotAssessed"/>. Bounds are inclusive. Qualitative: a stated normal
/// text equal to the value (trimmed, ordinal, ignoring case) is Normal, otherwise
/// <see cref="ResultFlag.Abnormal"/>; no normal text is NotAssessed.
/// </summary>
public static class ReferenceRangeClassifier
{
    public static ResultFlag ClassifyNumeric(decimal value, decimal? low, decimal? high)
    {
        if (low is null && high is null) return ResultFlag.NotAssessed;
        if (low is { } l && value < l) return ResultFlag.Low;
        if (high is { } h && value > h) return ResultFlag.High;
        return ResultFlag.Normal;
    }

    public static ResultFlag ClassifyQualitative(string? value, string? normalText)
    {
        if (string.IsNullOrWhiteSpace(normalText)) return ResultFlag.NotAssessed;

        return string.Equals((value ?? "").Trim(), normalText.Trim(), StringComparison.OrdinalIgnoreCase)
            ? ResultFlag.Normal
            : ResultFlag.Abnormal;
    }
}
