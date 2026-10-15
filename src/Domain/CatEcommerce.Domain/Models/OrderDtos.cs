using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-ORD-001 §11.3 -- a line of an order as placement hands it to the aggregate: the priced <c>QuoteLine</c>, in decimals at the aggregate's edge.</summary>
public sealed record OrderLineDto(
    Guid LineId, int LineNumber, Guid SkuId, Guid ProductId, string ProductName, string VariantLabel, string? Barcode, ProductReturnClass ReturnClass, bool IsBundle,
    Guid? CatId, int Quantity, decimal UnitPrice, decimal LineSubtotal, decimal LineDiscount, decimal OrderDiscountShare, Guid TaxClassId, decimal TaxRatePercent,
    decimal TaxAmount, decimal LineTotal, IReadOnlyList<OrderLineComponent> Components);

/// <summary>Everything <c>OrderAggregate.Place</c> needs. The order number comes from the sequence (V1); the totals must re-sum from the lines. <c>DeliveryTaxAmount</c> (FU1 D3/ORD-56, stored on the order at placement) is the part of <c>Totals.TaxTotal</c> that is tax on the delivery charge rather than on a line; default 0.</summary>
public sealed record PlaceOrderDto(
    string OrderNumber,
    OrderChannel Channel,
    Guid? CustomerId,
    Guid BranchId,
    FulfilmentMethod Fulfilment,
    OrderPaymentMethod PaymentMethod,
    string CurrencyCode,
    FulfilmentDetails Details,
    IReadOnlyList<OrderLineDto> Lines,
    OrderTotals Totals,
    Guid? DiscountCodeId,
    string? DiscountCodeText,
    string QuoteFingerprint,
    Guid? IdempotencyKey,
    DateTimeOffset PlacedOn,
    UserContext UserContext,
    Guid CorrelationId,
    decimal DeliveryTaxAmount = 0m);

/// <summary>One batch allocation Inventory chose for a line (or, for a bundle, a component of it).</summary>
public sealed record AllocationDto(Guid LineId, Guid SkuId, Guid BatchId, string LotNumber, Guid LocationId, decimal Quantity, DateOnly? BatchExpiresOn);

/// <summary>Money received at hand-over: a pay-on-collection tender. An in-store card needs the terminal slip reference (C-5: a method, an amount and a
/// reference -- never a card number).</summary>
public sealed record CollectionPaymentDto(TenderMethod Method, decimal Amount, string? Reference);
