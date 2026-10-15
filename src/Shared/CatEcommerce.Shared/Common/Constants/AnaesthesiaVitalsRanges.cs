namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-SRG-001 §10.3 -- feline plausibility ranges for the anaesthesia log. A value outside is refused unless the caller confirms it, and the
/// confirmation is stored on the entry. These are guard rails against a typo, not clinical limits.</summary>
public static class AnaesthesiaVitalsRanges
{
    public static readonly (decimal Min, decimal Max) HeartRateBpm = (60, 260);
    public static readonly (decimal Min, decimal Max) RespiratoryRatePerMin = (4, 80);
    public static readonly (decimal Min, decimal Max) SpO2Percent = (70, 100);
    public static readonly (decimal Min, decimal Max) EtCo2MmHg = (15, 80);
    public static readonly (decimal Min, decimal Max) TemperatureCelsius = (32.0m, 42.0m);
    public static readonly (decimal Min, decimal Max) SystolicMmHg = (40, 250);
    public static readonly (decimal Min, decimal Max) MeanArterialMmHg = (30, 200);

    public static bool IsPlausible((decimal Min, decimal Max) range, decimal value) => value >= range.Min && value <= range.Max;
}
