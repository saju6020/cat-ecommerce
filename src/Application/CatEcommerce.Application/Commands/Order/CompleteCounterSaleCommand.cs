using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Order;

/// <summary>One scanned line of a counter sale. A prescription food names the cat it is for (the customer is then required).</summary>
public sealed record CounterSaleLineInput(Guid SkuId, int Quantity, Guid? CatId);

/// <summary>One tender taken at the till: <c>Cash</c> is the money handed over (the change is worked out), <c>InStoreCard</c> is charged at face value and carries the
/// terminal slip reference -- never a card number (C-5).</summary>
public sealed record CounterTenderInput(TenderMethod Method, decimal Amount, string? Reference);

/// <summary>
/// HO-ORD-001 §11.1 C26 (US-ORD-16) -- complete a counter sale in the staff member's <b>active branch</b>: price the lines, take the stock off the shelf FEFO, invoice
/// and take payment, and finish the order (<c>PendingPayment -> Confirmed -> Delivered</c>) in one transaction. The sale records the buyer's name and mobile (ADR-216):
/// an existing customer with that mobile, or a new passwordless one, carries the order and the invoice.
/// </summary>
public sealed class CompleteCounterSaleCommand : Command
{
    public required Guid OrderId { get; init; }
    public required Guid IdempotencyKey { get; init; }
    public required Guid BranchId { get; init; }
    /// <summary>The customer's name and mobile number, both required (ADR-216, owner decision 6 of 2026-10-09). An active customer with this primary mobile is the buyer; otherwise a
    /// passwordless customer is registered with this name.</summary>
    public required string CustomerName { get; init; }
    public required string CustomerMobile { get; init; }
    public required IReadOnlyList<CounterSaleLineInput> Lines { get; init; }
    public string? DiscountCode { get; init; }
    public required IReadOnlyList<CounterTenderInput> Tenders { get; init; }
}

public sealed record CounterSaleResult(Guid OrderId, string OrderNumber, string InvoiceNumber, decimal GrandTotal, decimal ChangeDue, string CurrencyCode);
