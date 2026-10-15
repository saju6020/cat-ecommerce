using System.Globalization;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Order;
using CatEcommerce.Api.Filters;
using CatEcommerce.Api.Idempotency;
using CatEcommerce.Api.Ordering;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.CommandHandlers.Order;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-ORD-001 §12.4 -- the signed-in customer's orders. Every route resolves the order <b>by id and caller</b>: someone else's order is <c>404</c>, the same
/// answer as one that does not exist (§12.3). Placement is storefront (maintenance-gated), rate limited (<c>checkout</c>, NFR-SEC-6) and idempotent at the
/// HTTP edge (<c>Idempotency-Key</c>, ADR-202, US-ORD-18 AC1); it answers <c>201</c> with the real outcome, never <c>202</c> (ADR-013).
/// </summary>
[ApiController]
[Route("api/customers/me/orders")]
[RequireCustomerSession]
public sealed class CustomerOrdersController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpPost]
    [StorefrontEndpoint]
    [EnableRateLimiting(RateLimitPolicies.Checkout)]
    [Idempotent]
    [ConflictAsQuoteChanged]
    public async Task<IActionResult> Place([FromBody] PlaceOrderRequest request)
    {
        if (!EnumQueryParameter.TryParse<FulfilmentMethod>(request.Fulfilment, out var method) || method is null || method == FulfilmentMethod.Counter)
            return BadRequest(new { error = "fulfilment must be Delivery or Pickup." });

        if (!EnumQueryParameter.TryParse<OrderPaymentMethod>(request.PaymentMethod, out var payment) || payment is null || payment == OrderPaymentMethod.InStore)
            return BadRequest(new { error = "paymentMethod must be CashOnDelivery, BankTransfer or PayOnCollection." });

        if (!EnumQueryParameter.TryParse<DeliveryServiceLevel>(request.ServiceLevel, out var level)) return BadRequest(new { error = "serviceLevel must be Standard or Express." });

        DateOnly? slotDate = null;
        if (!string.IsNullOrWhiteSpace(request.SlotDate))
        {
            if (!DateOnly.TryParseExact(request.SlotDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return BadRequest(new { error = "slotDate must be yyyy-MM-dd." });
            slotDate = parsed;
        }

        TimeOnly? slotStart = null;
        if (!string.IsNullOrWhiteSpace(request.SlotWindowStart))
        {
            if (!TimeOnly.TryParseExact(request.SlotWindowStart, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return BadRequest(new { error = "slotWindowStart must be HH:mm." });
            slotStart = parsed;
        }

        // The idempotency filter has already required and parsed the header (ADR-202); an unparsable one never reaches here.
        var key = Guid.Parse(Request.Headers[IdempotencyRules.HeaderName].ToString());
        var user = userContextProvider.GetUserContext();

        var response = await dispatcher.SendAsync(new PlaceOrderCommand
        {
            OrderId = Guid.NewGuid(), IdempotencyKey = key, FulfilmentMethod = method, DeliveryAddressId = request.DeliveryAddressId, ServiceLevel = level,
            SlotDate = slotDate, SlotWindowStart = slotStart, PickupBranchId = request.PickupBranchId, PaymentMethod = payment, DiscountCode = request.DiscountCode,
            RecipientName = request.RecipientName, RecipientPhone = request.RecipientPhone, QuoteFingerprint = request.QuoteFingerprint ?? string.Empty,
            UserContext = user, CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return OrderingResults.Failure(this, response);

        var placed = (PlaceOrderResult)response.Result;
        var body = new
        {
            orderId = placed.OrderId,
            orderNumber = placed.OrderNumber,
            status = placed.Status,
            grandTotal = OrderMoney.Of(placed.GrandTotal, placed.CurrencyCode),
            paymentDueBy = placed.PaymentDueBy,
            transferInstructions = placed.TransferInstructions is { } t
                ? new
                {
                    accountName = t.AccountName, accountNumber = t.AccountNumber, bankName = t.BankName, branchName = t.BranchName, routingNumber = t.RoutingNumber,
                    paymentReference = t.PaymentReference, amountDue = OrderMoney.Of(t.AmountDue, t.CurrencyCode),
                }
                : null,
        };

        return Created($"/api/customers/me/orders/{placed.OrderId}", body);
    }

    /// <summary>The caller's orders, newest first (<c>status</c> optional). Portal read: never maintenance-gated.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        Response.Headers.CacheControl = "no-store";
        if (!EnumQueryParameter.TryParse<OrderStatus>(status, out var parsed)) return BadRequest(new { error = "status is not an order status." });

        var query = new GetCustomerOrdersQuery { CustomerId = userContextProvider.GetUserContext().UserId, Status = parsed, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> Get(Guid orderId)
    {
        Response.Headers.CacheControl = "no-store";
        var user = userContextProvider.GetUserContext();
        var response = await dispatcher.GetResultAsync(new GetCustomerOrderDetailQuery { CustomerId = user.UserId, OrderId = orderId, UserContext = user, CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    /// <summary>US-ORD-19 -- the customer reports the reference their bank gave the transfer. Own order only (<c>404</c> otherwise).</summary>
    [HttpPost("{orderId:guid}/transfer-reference")]
    public async Task<IActionResult> SubmitTransferReference(Guid orderId, [FromBody] SubmitTransferReferenceRequest request)
    {
        var response = await dispatcher.SendAsync(new SubmitTransferReferenceCommand
        {
            OrderId = orderId, Reference = request.Reference ?? string.Empty, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    /// <summary>US-ORD-12 -- cancel before packing; <c>400 CancellationNotAvailable</c> afterwards ("contact the shop"). Not maintenance-gated.</summary>
    [HttpPost("{orderId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid orderId)
    {
        var response = await dispatcher.SendAsync(new CancelOrderCommand
        { OrderId = orderId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    /// <summary>US-ORD-13 -- ask to send items back. A request with any refused line is refused whole, with a reason per line (<c>lines[i]</c>); otherwise it waits for staff approval.
    /// Own order only (<c>404</c> otherwise). Not maintenance-gated.</summary>
    [HttpPost("{orderId:guid}/returns")]
    public async Task<IActionResult> RequestReturn(Guid orderId, [FromBody] RequestReturnRequest request)
    {
        if (!ReturnControllerSupport.TryMapLines(request.Lines, out var lines, out var error)) return BadRequest(new { error });

        var response = await dispatcher.SendAsync(new RequestReturnCommand
        { ReturnRequestId = Guid.NewGuid(), OrderId = orderId, Lines = lines, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        if (!response.IsSuccess) return OrderingResults.Failure(this, response);

        var result = (RequestReturnResult)response.Result;
        return Created($"/api/customers/me/returns/{result.ReturnRequestId}", new { returnRequestId = result.ReturnRequestId, status = result.Status });
    }

    /// <summary>US-ORD-19 AC3 -- order again: the available lines go back into the cart at today's prices; no order is created. Idempotent at the edge.</summary>
    [HttpPost("{orderId:guid}/reorder")]
    [StorefrontEndpoint]
    [Idempotent]
    public async Task<IActionResult> Reorder(Guid orderId)
    {
        var response = await dispatcher.SendAsync(new ReorderCommand
        { SourceOrderId = orderId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        if (!response.IsSuccess) return OrderingResults.Failure(this, response);

        var result = (ReorderResult)response.Result;
        return Ok(new { addedCount = result.AddedCount, unavailable = result.Unavailable.Select(u => new { productName = u.ProductName, reason = u.Reason }) });
    }
}
