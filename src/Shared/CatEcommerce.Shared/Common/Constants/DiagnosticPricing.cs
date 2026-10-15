using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-BIL-001 Phase J (owner decision 2026-10-14) -- which registry setting prices a diagnostic test of each <see cref="DiagnosticKind"/>.
/// Exhaustive on purpose: a new kind without a price key throws here, and <c>DiagnosticPricingTests</c> proves every kind maps to a registered setting.
/// </summary>
public static class DiagnosticPricing
{
    public static string SettingKeyFor(DiagnosticKind kind)
    {
        if (kind == DiagnosticKind.BloodWork) return SettingKeys.ClinicalDiagnosticPriceBloodWork;
        if (kind == DiagnosticKind.Urinalysis) return SettingKeys.ClinicalDiagnosticPriceUrinalysis;
        if (kind == DiagnosticKind.FelvFivTest) return SettingKeys.ClinicalDiagnosticPriceFelvFivTest;
        if (kind == DiagnosticKind.Imaging) return SettingKeys.ClinicalDiagnosticPriceImaging;
        if (kind == DiagnosticKind.Other) return SettingKeys.ClinicalDiagnosticPriceOther;
        throw new ArgumentOutOfRangeException(nameof(kind), kind.DisplayName, "No price setting is defined for this diagnostic kind.");
    }
}
