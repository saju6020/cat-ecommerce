using System.Globalization;
using System.Text.RegularExpressions;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-IMP-001 §10.3 -- limits and shape rules shared by the aggregates, validators and reads.</summary>
public static partial class InventoryRules
{
    public const int MaxPurchaseOrderLines = 200;
    public const int MaxReceiptLines = 200;
    public const int MaxTransferLines = 100;
    public const int MaxAdjustmentLines = 50;
    public const int MaxCountLines = 2000;
    public const int MaxCountSkus = 200;
    public const int MaxDocuments = 50;
    public const int MaxHsCodes = 20;
    public const int MaxCostComponents = 20;
    public const int MaxExchangeRates = 10;
    public const int MaxLandedCostLines = 500;
    public const int MaxStatusHistory = 20;
    public const int MaxConsignmentPurchaseOrders = 20;
    public const int MaxBreachBatches = 2000;
    public const long MaxDocumentBytes = 20L * 1024 * 1024;
    public const int LedgerQueryMaxRangeDays = 366;
    public const int LedgerCsvMaxRows = 50_000;
    public const int MaxPageSize = 100;

    /// <summary>The <c>resourceName</c> a handler puts on the error that means "the record this request ADDRESSES does not exist (or is another branch's)" -- the
    /// one failure an endpoint answers with 404. A missing record merely REFERENCED in the body (a supplier, a deliver-to location) is a 400.</summary>
    public const string NotFoundResource = "Inventory.NotFound";
    public const int ReasonMinLength = 5;
    public const int NoteMaxLength = 500;
    public const int MaxLocationCodeLength = 20;
    public const int MaxLotNumberLength = 60;
    public const decimal MinTemperatureC = -40m;
    public const decimal MaxTemperatureC = 60m;

    /// <summary>US-IMP-12 / D-9 stand-in: HO-XCT-001's <c>retention.financialYears</c> default, until that setting is registered.</summary>
    public const int DefaultFinancialRetentionYears = 6;

    public static readonly IReadOnlyList<string> AllowedDocumentContentTypes = ["application/pdf", "image/jpeg", "image/png"];

    /// <summary>§8.5: <c>^\d{4}(\.?\d{2}){0,3}$</c>.</summary>
    public static bool IsValidHsCode(string? code) => !string.IsNullOrWhiteSpace(code) && HsCodePattern().IsMatch(code);

    /// <summary>ISO-3166 alpha-2 validated with <see cref="RegionInfo"/> (§8.5), after the Shop country list.</summary>
    public static bool IsValidCountryCode(string? code)
    {
        if (code is null || code.Length != 2 || !code.All(c => c is >= 'A' and <= 'Z')) return false;
        if (CountryCodes.IsValid(code)) return true;
        try { return string.Equals(new RegionInfo(code).TwoLetterISORegionName, code, StringComparison.Ordinal); }
        catch (ArgumentException) { return false; }
    }

    /// <summary>The same three-upper-letter shape as HO-ADM-001's <c>CurrencyCode</c> setting type.</summary>
    public static bool IsValidCurrencyCode(string? code) => code is { Length: 3 } && code.All(c => c is >= 'A' and <= 'Z');

    [GeneratedRegex(@"^\d{4}(\.?\d{2}){0,3}$")]
    private static partial Regex HsCodePattern();
}
