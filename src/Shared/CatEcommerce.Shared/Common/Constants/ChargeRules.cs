using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-BIL-001 §8.2/§8.3 -- charge field bounds and the default business line per source.</summary>
public static class ChargeRules
{
    public const int MaxSourceLineRefLength = 100;
    public const int MaxReportingTagLength = 50;
    public const int MaxVoidReasonLength = 500;
    public const string DefaultSourceLineRef = "main";
    public const int MaxQuantityDecimals = 3;
    public const int MaxMoneyDecimals = 2;

    /// <summary>§8.2: Appointment/Visit/Dispense/Surgery -> Clinic; Order -> Shop; BoardingStay (and
    /// the daycare visit HO-BRD-001 adds) -> Boarding. <c>Manual</c> has no default -- the desk charge
    /// is Clinic unless the caller names another line.</summary>
    public static BusinessLine DefaultBusinessLine(ChargeSourceKind sourceKind)
    {
        if (sourceKind == ChargeSourceKind.Order) return BusinessLine.Shop;
        if (sourceKind == ChargeSourceKind.BoardingStay || sourceKind == ChargeSourceKind.DaycareVisit) return BusinessLine.Boarding;
        return BusinessLine.Clinic;
    }

    /// <summary>§8.2: the default mapping is overridable only by <c>Manual</c>.</summary>
    public static bool MayOverrideBusinessLine(ChargeSourceKind sourceKind) => sourceKind == ChargeSourceKind.Manual;
}
