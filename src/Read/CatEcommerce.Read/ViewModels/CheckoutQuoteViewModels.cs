namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-ORD-001 §12.4 -- the checkout quote's lines, every component of the price its own field (US-ORD-04 AC1). <c>LineRef</c> is the cart line.</summary>
public sealed record CheckoutQuoteLineViewModel(
    Guid LineRef, Guid SkuId, string ProductName, string VariantLabel, CartCatViewModel? Cat, int Quantity, MoneyViewModel UnitPrice, MoneyViewModel LineSubtotal,
    MoneyViewModel LineDiscount, MoneyViewModel OrderDiscountShare, decimal TaxRatePercent, MoneyViewModel TaxAmount, MoneyViewModel LineTotal);

public sealed record ExcludedLineViewModel(Guid LineRef, Guid SkuId, string ProductName, CartCatViewModel? Cat, int Quantity, string ReasonCode);

public sealed record CheckoutChangeViewModel(string Kind, Guid LineRef, string ProductName, string? Before, string? After, string Message);

public sealed record DeliverySlotViewModel(string Date, string WindowStart, string WindowEnd, string Label);

public sealed record QuoteServiceLevelViewModel(string Level, MoneyViewModel Charge, int LeadTimeDays, IReadOnlyList<DeliverySlotViewModel> Slots);

/// <summary><c>Available</c> is false outside every zone (<c>UnavailableReason = AddressOutsideServedZones</c>); pickup is then offered through
/// <c>PickupBranches</c>.</summary>
public sealed record QuoteDeliveryViewModel(bool Available, string? UnavailableReason, Guid? ZoneId, string? ZoneName, IReadOnlyList<QuoteServiceLevelViewModel> ServiceLevels);

public sealed record PickupBranchViewModel(Guid Id, string Name, string? Address, string? Phone);

public sealed record PaymentOptionViewModel(string Method, bool Available, string? UnavailableReason, string? Message);

public sealed record QuoteDiscountViewModel(string Code, bool Applied, string? ReasonCode, string? Message);

public sealed record QuoteTotalsViewModel(
    MoneyViewModel Subtotal, MoneyViewModel LineDiscountTotal, MoneyViewModel OrderDiscount, MoneyViewModel DeliveryCharge, MoneyViewModel TaxTotal, MoneyViewModel GrandTotal);

public sealed record PendingPolicyViewModel(string Key, int VersionNumber, string Title);

/// <summary>The quote (ADR-200): computed on every request, never stored. The customer acknowledges it by sending back <c>Fingerprint</c>.</summary>
public sealed record CheckoutQuoteViewModel(
    IReadOnlyList<CheckoutQuoteLineViewModel> Lines, IReadOnlyList<ExcludedLineViewModel> ExcludedLines, IReadOnlyList<CheckoutChangeViewModel> Changes,
    QuoteDeliveryViewModel Delivery, string? SelectedServiceLevel, IReadOnlyList<PickupBranchViewModel> PickupBranches, IReadOnlyList<PaymentOptionViewModel> PaymentOptions,
    QuoteDiscountViewModel? Discount, QuoteTotalsViewModel Totals, IReadOnlyList<PendingPolicyViewModel> PendingPolicies, bool CanPlace, IReadOnlyList<string> BlockingReasons,
    string Fingerprint, string CurrencyCode);
