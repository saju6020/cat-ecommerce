using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-RPT-001 section 8.6 / ADR-283 -- the business line of revenue, <b>pure</b>. HO-BIL-001 stores the line on every invoice line (<c>BusinessLine</c>: Clinic, Shop, Boarding) at the moment the charge is
/// captured, so a combined invoice (FR-ORD-19) is classified per line and never per invoice. Reporting only reads it. A value it does not know -- a line added by a later area -- is <c>Other</c>: shown,
/// never dropped (U-D1). The platform <c>BusinessLine</c> enumeration is HO-BIL-001's and gains no member from Reporting.
/// </summary>
public static class BusinessLineRules
{
    public const string Other = "Other";

    public static string Classify(int businessLine)
    {
        if (businessLine == BusinessLine.Clinic.Value) return nameof(BusinessLine.Clinic);
        if (businessLine == BusinessLine.Shop.Value) return nameof(BusinessLine.Shop);
        if (businessLine == BusinessLine.Boarding.Value) return nameof(BusinessLine.Boarding);
        return Other;
    }

    public static bool IsShop(int businessLine) => businessLine == BusinessLine.Shop.Value;
}
