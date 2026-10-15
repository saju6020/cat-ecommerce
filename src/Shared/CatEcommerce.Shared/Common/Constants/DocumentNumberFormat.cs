using System.Globalization;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-BIL-001 §8.7. <c>{prefix}-{branchCode}-{fyCompact}-{sequence:D6}</c> -- e.g.
/// <c>INV-MAIN-2627-000123</c>, <c>CN-MAIN-2627-000007</c>. Pure.</summary>
public static class DocumentNumberFormat
{
    public static string Prefix(BillingDocumentKind kind) => kind == BillingDocumentKind.CreditNote ? "CN" : "INV";

    public static string FinancialYearCompact(string financialYear)
    {
        var parts = financialYear.Split('-');
        if (parts.Length != 2) return financialYear;

        var startYear = int.Parse(parts[0], CultureInfo.InvariantCulture);
        return $"{(startYear % 100).ToString("D2", CultureInfo.InvariantCulture)}{parts[1]}";
    }

    public static string Format(BillingDocumentKind kind, string branchCode, string financialYear, long sequence) =>
        $"{Prefix(kind)}-{branchCode}-{FinancialYearCompact(financialYear)}-{sequence.ToString("D6", CultureInfo.InvariantCulture)}";
}
