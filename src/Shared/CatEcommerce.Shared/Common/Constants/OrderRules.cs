namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-ORD-001 §10.3 -- the order bounds, in one place.</summary>
public static class OrderRules
{
    public const int MaxLines = 50;
    public const int MaxQuantityPerLineHardCap = 999;
    public const int MaxReasonLength = 500;
    public const int MaxStatusHistory = 30;
    public const int MaxStatusNoteLength = 200;
    public const int MaxOrderNumberLength = 20;
    public const int MaxTransferReferenceLength = 100;
    public const int MaxTrackingNumberLength = 100;
    public const int MaxRecipientNameLength = 200;
    public const int MaxRecipientPhoneLength = 30;
    public const int MaxPaymentRefs = 10;
    public const int MaxAllocationsPerLine = 10;
    public const int MaxCreditNotes = 10;
    public const int MaxNameLength = 100;
    public const int MaxTerminalReferenceLength = 50;

    /// <summary>The order number: <c>ORD-{yyyy}-{n:D6}</c> from <c>ordering.order_number_seq</c> (gaps are allowed -- an order number
    /// is not an invoice number).</summary>
    public const string OrderNumberFormat = "ORD-{0:D4}-{1:D6}";

    public static string FormatOrderNumber(int year, long sequence) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, OrderNumberFormat, year, sequence);
}

/// <summary>HO-ORD-001 §8.8 -- delivery-zone configuration bounds.</summary>
public static class DeliveryZoneRules
{
    public const int MaxNameLength = 100;
    public const int MaxCoverageRows = 200;
    public const int MaxPostalCodesPerCoverage = 100;
    public const int MaxServiceLevels = 2;
    public const int MaxWeightBands = 10;
    public const int MaxSlotWindows = 21;
    public const int MaxLeadTimeDays = 14;
    public const int MaxDistrictLength = 100;
    public const int MaxPostalCodeLength = 20;
    public const int SlotLookaheadDays = 7;
}

/// <summary>HO-ORD-001 §8.15 -- discount code bounds.</summary>
public static class DiscountCodeRules
{
    public const int MinCodeLength = 3;
    public const int MaxCodeLength = 30;
    public const int MaxScopeIds = 200;
    public const int MaxDescriptionLength = 200;
    public const string CodePattern = "^[A-Z0-9-]+$";
}
