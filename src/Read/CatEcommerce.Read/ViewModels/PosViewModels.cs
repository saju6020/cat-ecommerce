namespace CatEcommerce.Read.ViewModels;

/// <summary>The next batch FEFO would take for one unit of a SKU at the branch -- a preview, nothing is allocated.</summary>
public sealed record PosFefoBatchViewModel(Guid BatchId, string LotNumber, DateOnly? ExpiresOn, Guid LocationId, string Quantity);

/// <summary>HO-ORD-001 §12.4 -- a SKU found at the till. <c>Sellable</c> is what the branch can sell now; <c>FefoBatches</c> is empty for a bundle (its components are
/// picked individually) or a SKU with nothing to sell.</summary>
public sealed record PosSkuViewModel(
    Guid SkuId, Guid ProductId, string ProductName, string VariantLabel, string? Barcode, MoneyViewModel? UnitPrice, int Sellable, bool RequiresPrescription, bool IsBundle,
    IReadOnlyList<PosFefoBatchViewModel> FefoBatches);

public sealed record CounterQuoteViewModel(
    IReadOnlyList<CheckoutQuoteLineViewModel> Lines, IReadOnlyList<ExcludedLineViewModel> ExcludedLines, IReadOnlyList<CheckoutChangeViewModel> Changes,
    QuoteDiscountViewModel? Discount, QuoteTotalsViewModel Totals, bool CanSell, IReadOnlyList<string> BlockingReasons, string CurrencyCode);

public sealed record ReceiptLineViewModel(
    string Description, int Quantity, MoneyViewModel UnitPrice, MoneyViewModel LineDiscount, decimal TaxRatePercent, MoneyViewModel TaxAmount, MoneyViewModel LineTotal);

public sealed record ReceiptTenderViewModel(string Method, MoneyViewModel Amount);

/// <summary>The receipt: branch and legal identifiers (from Billing's invoice -- NFR-LEG-3), numbers, lines, totals, tenders, change, cashier and the time in the branch's
/// zone, plus <c>Text</c> -- the same content laid out in 48 columns for an 80 mm printer.</summary>
public sealed record ReceiptViewModel(
    Guid OrderId, string OrderNumber, string InvoiceNumber, string BranchName, string? BranchAddress, string? BranchPhone, string? LegalName, string? TaxRegistrationNumber,
    DateTimeOffset SoldOn, string SoldOnLocal, string BranchTimeZoneId, string Cashier, string? CustomerName, IReadOnlyList<ReceiptLineViewModel> Lines, QuoteTotalsViewModel Totals,
    IReadOnlyList<ReceiptTenderViewModel> Tenders, MoneyViewModel ChangeDue, string Text);
