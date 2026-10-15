using System.Globalization;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>One vital sign's unit, absolute (refused outside) and plausible (confirm outside) range
/// and precision (HO-EMR-001 §8.3, ADR-120). A <c>null</c> plausible bound means the sign has no
/// confirm tier -- only the absolute range applies.</summary>
public sealed record VitalSignRange(
    VitalSign Sign, string Unit, decimal AbsoluteMin, decimal AbsoluteMax,
    decimal? PlausibleMin, decimal? PlausibleMax, int DecimalPlaces);

/// <summary>
/// HO-EMR-001 §8.3 -- <b>clinical lead to confirm every number here (OQ-3)</b>. Changing a range or a
/// scale is a one-file edit; nothing else in the solution hard-codes them (the read side returns this
/// same table from <c>GET /api/staff/clinical/vital-sign-rules</c>, so a client warns from the server's
/// numbers while the server remains the decision).
/// </summary>
public static class VitalSignRules
{
    public static readonly IReadOnlyList<VitalSignRange> All =
    [
        new(VitalSign.Weight, "kg", 0.10m, 30.00m, 0.30m, 12.00m, 2),
        new(VitalSign.Temperature, "°C", 25.0m, 45.0m, 35.0m, 41.5m, 1),
        new(VitalSign.HeartRate, "bpm", 20m, 400m, 100m, 260m, 0),
        new(VitalSign.RespiratoryRate, "breaths/min", 4m, 150m, 12m, 80m, 0),
        // 9-point scale (OQ-3).
        new(VitalSign.BodyConditionScore, "/9", 1m, 9m, null, null, 0),
        // 0-10 scale (OQ-3).
        new(VitalSign.PainScore, "/10", 0m, 10m, null, null, 0),
    ];

    public static VitalSignRange For(VitalSign sign) => All.First(r => r.Sign == sign);

    /// <summary>The value formatted to the sign's stated precision, e.g. <c>"4.20"</c> (UI-8).</summary>
    public static string Format(VitalSign sign, decimal value) =>
        value.ToString("F" + For(sign).DecimalPlaces, CultureInfo.InvariantCulture);
}
