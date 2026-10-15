using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Models;

/// <summary>The state of a prescription line at quote time (US-SHP-10 AC3, US-ORD-03 AC2).</summary>
public enum PrescriptionLineStatus
{
    /// <summary>Not a prescription SKU.</summary>
    NotRequired,

    /// <summary>The gate says the cat is authorised today.</summary>
    Authorised,

    /// <summary>A cat is chosen but the authorisation is missing or lapsed.</summary>
    NotAuthorised,

    /// <summary>No cat is set on the line.</summary>
    CatRequired,
}

/// <summary>One cart line with everything the pure pricing needs about its SKU: the catalogue snapshot, the quantity that can be sold at the
/// fulfilling branch, the tax rate in force and the prescription status. Loaded by <c>ICheckoutInputsLoader</c>.</summary>
public sealed record CheckoutLineInput(
    Guid LineId, OrderCatalogueSku Sku, Guid? CatId, int CartQuantity, decimal PriceWhenAdded, decimal Sellable, decimal? TaxRatePercent, PrescriptionLineStatus Prescription);

/// <summary>What the customer chose on the checkout page. Everything is optional until it is needed: a quote can be asked for while the customer is
/// still choosing.</summary>
public sealed record FulfilmentChoice(
    FulfilmentMethod Method, Guid? AddressId, DeliveryServiceLevel? ServiceLevel, DateOnly? SlotDate, TimeOnly? SlotWindowStart, Guid? PickupBranchId,
    OrderPaymentMethod? PaymentMethod, string? DiscountCode);

public sealed record AddressFacts(Guid AddressId, string Line1, string? Line2, string City, string District, string? PostalCode, string? DeliveryInstructions);

public sealed record PickupBranchFact(Guid BranchId, string Name, string? Address, string? Phone);

public sealed record ZoneServiceFacts(DeliveryServiceLevel Level, bool IsActive, ZoneChargeRules Charge, int LeadTimeDays, IReadOnlyList<SlotWindow> SlotWindows);

/// <summary>The zone an address resolved to, with its service levels and cash-on-delivery rules.</summary>
public sealed record DeliveryZoneFacts(
    Guid ZoneId, string Name, Guid BranchId, bool CodEnabled, decimal? CodMaxOrderValue, IReadOnlyList<ZoneServiceFacts> Services);

/// <summary>The tax a delivery charge carries (HO-ORD-001-FU1 D3): the Billing tax class it is invoiced under and that class's rate in force. <c>RatePercent</c> is 0
/// until an Administrator sets a rate on the <see cref="ClassCode"/> tax class (default 0 %).</summary>
public sealed record DeliveryTaxFacts(Guid? TaxClassId, decimal RatePercent)
{
    /// <summary>The code of the tax class that governs delivery charges (seeded at 0 %; the rate is changed through the existing tax configuration).</summary>
    public const string ClassCode = "DELIVERY";
}

public sealed record PendingPolicyFact(string Key, int VersionNumber, string Title);

/// <summary>The branch-level payment switches (<c>orders.*</c> settings and the bank account), already resolved for the fulfilling branch.</summary>
public sealed record PaymentSettingsFacts(bool CodEnabled, decimal CodMaxOrderValue, bool BankTransferEnabled, bool BankAccountActive, bool PayOnCollectionEnabled);

/// <summary>Everything <c>CheckoutPricing</c> needs, gathered by <c>ICheckoutInputsLoader</c> (HO-ORD-001 §8.14). The pricing is a pure function of this value
/// -- no clock, no I/O -- so the same inputs always give the same quote and the same fingerprint (ADR-200).</summary>
public sealed record CheckoutInputs(
    DateTimeOffset Now,
    DateOnly TodayLocal,
    string CurrencyCode,
    IReadOnlyList<CheckoutLineInput> Lines,
    FulfilmentChoice Choice,
    AddressFacts? Address,
    DeliveryZoneFacts? Zone,
    IReadOnlySet<DateOnly> OpenDates,
    IReadOnlyList<PickupBranchFact> PickupBranches,
    string? DiscountCodeText,
    DiscountRuleSet? Discount,
    CustomerDiscountFacts CustomerFacts,
    PaymentSettingsFacts Payment,
    IReadOnlyList<PendingPolicyFact> PendingPolicies,
    Guid FulfilmentBranchId,
    DeliveryTaxFacts? DeliveryTax = null);

/// <summary>A line of the quote -- and, once placed, of the order. Amounts are minor units (ADR-198).</summary>
public sealed record QuoteLine(
    Guid LineRef, Guid SkuId, Guid ProductId, string ProductName, string VariantLabel, string? Barcode, ProductReturnClass ReturnClass, bool IsBundle, Guid? CatId,
    int Quantity, MinorUnits UnitPrice, MinorUnits Subtotal, MinorUnits LineDiscount, MinorUnits OrderDiscountShare, Guid TaxClassId, decimal TaxRatePercent,
    MinorUnits TaxAmount, MinorUnits LineTotal, IReadOnlyList<CatalogueBundlePart> BundleComponents);

/// <summary>A cart line the quote leaves out (unavailable, or its prescription authorisation lapsed). It stays in the cart.</summary>
public sealed record ExcludedLine(Guid LineRef, Guid SkuId, string ProductName, Guid? CatId, int CartQuantity, string ReasonCode);

/// <summary>A difference since the item entered the cart, listed so the customer sees it before paying (US-ORD-02).</summary>
public sealed record CheckoutChange(CheckoutChangeKind Kind, Guid LineRef, string ProductName, string? Before, string? After, string Message);

public sealed record QuoteTotals(
    MinorUnits Subtotal, MinorUnits LineDiscountTotal, MinorUnits OrderDiscount, MinorUnits DeliveryCharge, MinorUnits TaxTotal, MinorUnits GrandTotal);

public sealed record QuoteServiceLevel(DeliveryServiceLevel Level, MinorUnits Charge, int LeadTimeDays, IReadOnlyList<DeliverySlot> Slots);

/// <summary>The delivery half of the quote. <c>Available</c> is false outside every zone (<c>AddressOutsideServedZones</c>), and pickup is then offered.</summary>
public sealed record QuoteDelivery(
    bool Available, string? UnavailableReason, Guid? ZoneId, string? ZoneName, Guid? BranchId, IReadOnlyList<QuoteServiceLevel> ServiceLevels);

public sealed record QuoteDiscount(string Code, bool Applied, string? ReasonCode, string? Message);

/// <summary>The checkout quote (HO-ORD-001 §8.5, ADR-200): the lines priced, what changed, the delivery and payment options, the totals, whether the
/// order can be placed and the fingerprint the customer must acknowledge. Computed, never stored.</summary>
public sealed record CheckoutQuote(
    IReadOnlyList<QuoteLine> Lines,
    IReadOnlyList<ExcludedLine> ExcludedLines,
    IReadOnlyList<CheckoutChange> Changes,
    QuoteDelivery Delivery,
    DeliveryServiceLevel? SelectedServiceLevel,
    IReadOnlyList<PickupBranchFact> PickupBranches,
    IReadOnlyList<PaymentOption> PaymentOptions,
    QuoteDiscount? Discount,
    QuoteTotals Totals,
    IReadOnlyList<PendingPolicyFact> PendingPolicies,
    bool CanPlace,
    IReadOnlyList<string> BlockingReasons,
    string Fingerprint,
    string CurrencyCode,
    Guid FulfilmentBranchId);
