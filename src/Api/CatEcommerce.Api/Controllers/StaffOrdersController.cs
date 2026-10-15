using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Order;
using CatEcommerce.Api.Idempotency;
using CatEcommerce.Api.Ordering;
using CatEcommerce.Application.CommandHandlers.Order;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-ORD-001 §12.4 -- the staff side of orders (payment, cancellation, the lists accountants work from). The branch is the staff member's <b>active</b> branch
/// (<c>X-Branch-Id</c>); an order of another branch does not exist for them. Money-recording writes are idempotent at the edge (<c>Idempotency-Key</c>,
/// ADR-202). Every action is gated by <c>[RequireAction]</c>; none is maintenance-gated (the back office keeps working, ADR-053).
/// </summary>
[ApiController]
[Route("api/staff/orders")]
public sealed class StaffOrdersController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : StaffOrderControllerBase(activeBranchAccessor)
{
    [HttpGet("awaiting-payment")]
    [RequireAction(Actions.Orders.ConfirmPayment)]
    public async Task<IActionResult> AwaitingPayment()
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetAwaitingPaymentOrdersQuery { BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpGet("refunds-due")]
    [RequireAction(Actions.Refunds.Issue)]
    public async Task<IActionResult> RefundsDue([FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var query = new GetRefundsDueQuery { BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    /// <summary>US-ORD-19 AC2 -- staff verified a transfer against the bank record. Above the approval threshold the confirmation is held for a second person
    /// (<c>awaitingApproval: true</c>).</summary>
    [HttpPost("{orderId:guid}/payment/bank-transfer")]
    [RequireAction(Actions.Orders.ConfirmPayment)]
    [Idempotent]
    public async Task<IActionResult> ConfirmBankTransfer(Guid orderId, [FromBody] ConfirmBankTransferRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ConfirmBankTransferCommand
        {
            OrderId = orderId, BranchId = branchId, ExpectedVersion = request.ExpectedVersion, Amount = request.Amount, BankReference = request.BankReference ?? string.Empty,
            ReceivedOn = request.ReceivedOn.ToUniversalTime(), UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return OrderingResults.Failure(this, response);

        var result = (ConfirmBankTransferResult)response.Result;
        return Ok(new { status = result.Status, awaitingApproval = result.AwaitingApproval });
    }

    [HttpPost("{orderId:guid}/payment/approve")]
    [RequireAction(Actions.Orders.ApprovePayment)]
    [Idempotent]
    public async Task<IActionResult> Approve(Guid orderId, [FromBody] ApproveHeldPaymentRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ApproveHeldPaymentCommand
        {
            OrderId = orderId, BranchId = branchId, ExpectedVersion = request.ExpectedVersion, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? Ok(new { status = ((ApproveHeldPaymentResult)response.Result).Status }) : OrderingResults.Failure(this, response);
    }

    [HttpPost("{orderId:guid}/payment/reject")]
    [RequireAction(Actions.Orders.ApprovePayment)]
    public async Task<IActionResult> Reject(Guid orderId, [FromBody] RejectHeldPaymentRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RejectHeldPaymentCommand
        {
            OrderId = orderId, BranchId = branchId, Note = request.Note ?? string.Empty, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    /// <summary>US-ORD-12 -- staff cancel (also while packing), with a reason.</summary>
    [HttpPost("{orderId:guid}/cancel")]
    [RequireAction(Actions.Orders.Cancel)]
    public async Task<IActionResult> Cancel(Guid orderId, [FromBody] CancelOrderRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new CancelOrderCommand
        {
            OrderId = orderId, BranchId = branchId, Note = request.Note, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    // ── the queue and the detail (Orders.View) ──────────────────────────────────────────────────────
    [HttpGet]
    [RequireAction(Actions.Orders.View)]
    public async Task<IActionResult> Queue(
        [FromQuery] string? status, [FromQuery] string? fulfilment, [FromQuery] string? paymentMethod, [FromQuery] string? channel, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to, [FromQuery] string? search, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        if (!EnumQueryParameter.TryParse<OrderStatus>(status, out var parsedStatus)) return BadRequest(new { error = "status is not an order status." });
        if (!EnumQueryParameter.TryParse<FulfilmentMethod>(fulfilment, out var parsedFulfilment)) return BadRequest(new { error = "fulfilment is not a fulfilment method." });
        if (!EnumQueryParameter.TryParse<OrderPaymentMethod>(paymentMethod, out var parsedPayment)) return BadRequest(new { error = "paymentMethod is not a payment method." });
        if (!EnumQueryParameter.TryParse<OrderChannel>(channel, out var parsedChannel)) return BadRequest(new { error = "channel is not an order channel." });
        if (search is { Length: > 100 }) return BadRequest(new { error = "search is too long." });

        var query = new GetStaffOrderQueueQuery
        {
            BranchId = branchId, Status = parsedStatus, Fulfilment = parsedFulfilment, PaymentMethod = parsedPayment, Channel = parsedChannel, From = from?.ToUniversalTime(), To = to?.ToUniversalTime(), Search = search,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        return Ok((await dispatcher.GetResultAsync(query)).Result);
    }

    [HttpGet("{orderId:guid}")]
    [RequireAction(Actions.Orders.View)]
    public async Task<IActionResult> Get(Guid orderId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetStaffOrderDetailQuery { BranchId = branchId, OrderId = orderId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    /// <summary>US-ORD-10 AC2 -- the packing slip: contents with batch numbers and the customer's details. Printing is the frontend's.</summary>
    [HttpGet("{orderId:guid}/packing-slip")]
    [RequireAction(Actions.Orders.View)]
    public async Task<IActionResult> PackingSlip(Guid orderId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetPackingSlipQuery { BranchId = branchId, OrderId = orderId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    // ── fulfilment (Orders.Fulfil) ──────────────────────────────────────────────────────────────────
    [HttpPost("{orderId:guid}/packing")]
    [RequireAction(Actions.Orders.Fulfil)]
    public async Task<IActionResult> StartPacking(Guid orderId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new StartPackingCommand
        { OrderId = orderId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    /// <summary>US-ORD-11 -- dispatch with a courier (tracking optional); <c>400 BatchRecalled</c> / <c>BatchExpired</c> / <c>OrderNotFullyAllocated</c> (BR-14).</summary>
    [HttpPost("{orderId:guid}/dispatch")]
    [RequireAction(Actions.Orders.Fulfil)]
    public async Task<IActionResult> Dispatch(Guid orderId, [FromBody] DispatchOrderRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new DispatchOrderCommand
        {
            OrderId = orderId, BranchId = branchId, CourierId = request.CourierId, TrackingNumber = request.TrackingNumber, UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    [HttpPost("{orderId:guid}/ready-for-collection")]
    [RequireAction(Actions.Orders.Fulfil)]
    public async Task<IActionResult> ReadyForCollection(Guid orderId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new MarkReadyForCollectionCommand
        { OrderId = orderId, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    /// <summary>US-ORD-11 AC2 -- add the tracking number later; the customer is told when it is the first.</summary>
    [HttpPut("{orderId:guid}/tracking")]
    [RequireAction(Actions.Orders.Fulfil)]
    public async Task<IActionResult> RecordTracking(Guid orderId, [FromBody] RecordTrackingNumberRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RecordTrackingNumberCommand
        {
            OrderId = orderId, BranchId = branchId, TrackingNumber = request.TrackingNumber ?? string.Empty, UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    /// <summary>US-ORD-20 -- hand-over: records the cash collected on delivery, or the payment taken at collection. Idempotent at the edge.</summary>
    [HttpPost("{orderId:guid}/delivery")]
    [RequireAction(Actions.Orders.RecordDelivery)]
    [Idempotent]
    public async Task<IActionResult> RecordDelivery(Guid orderId, [FromBody] RecordDeliveryRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        CodCollectionInput? cod = null;
        if (request.CodCollection is { } c)
        {
            if (!EnumQueryParameter.TryParse<CollectorKind>(c.CollectorKind, out var kind) || kind is null) return BadRequest(new { error = "collectorKind must be Courier or Staff." });
            cod = new CodCollectionInput(c.Amount, kind, c.CollectorName, c.CollectorStaffId, c.CollectedOn.ToUniversalTime());
        }

        CollectionPaymentInput? payment = null;
        if (request.CollectionPayment is { } p)
        {
            if (!EnumQueryParameter.TryParse<TenderMethod>(p.Method, out var method) || method is null) return BadRequest(new { error = "method must be Cash or InStoreCard." });
            payment = new CollectionPaymentInput(method, p.Amount, p.Reference);
        }

        var response = await dispatcher.SendAsync(new RecordDeliveryCommand
        {
            OrderId = orderId, BranchId = branchId, ExpectedVersion = request.ExpectedVersion, DeliveredOn = request.DeliveredOn.ToUniversalTime(), CodCollection = cod, CollectionPayment = payment,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    /// <summary>US-ORD-20 AC3 -- a parcel refused or undeliverable, or a pickup not collected.</summary>
    [HttpPost("{orderId:guid}/delivery-failure")]
    [RequireAction(Actions.Orders.Fulfil)]
    public async Task<IActionResult> RecordDeliveryFailure(Guid orderId, [FromBody] RecordDeliveryFailureRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (!EnumQueryParameter.TryParse<DeliveryFailureReason>(request.Reason, out var reason) || reason is null) return BadRequest(new { error = "reason must be Refused, Undeliverable or Uncollected." });

        var command = new RecordDeliveryFailureCommand
        {
            OrderId = orderId, BranchId = branchId, ReturnRequestId = Guid.NewGuid(), Reason = reason, Note = request.Note, UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        if (!response.IsSuccess) return OrderingResults.Failure(this, response);

        // A refused or undeliverable parcel creates the return it comes back on; an uncollected pickup never left the shop, so there is none.
        return reason == DeliveryFailureReason.Uncollected ? NoContent() : StatusCode(201, new { returnRequestId = command.ReturnRequestId });
    }

    /// <summary>US-IMP-07 AC2 (order half) -- move a line to a chosen batch while the order is confirmed or being packed; the reason is mandatory.</summary>
    [HttpPost("{orderId:guid}/lines/{lineId:guid}/reallocate")]
    [RequireAction(Actions.Orders.Reallocate)]
    public async Task<IActionResult> Reallocate(Guid orderId, Guid lineId, [FromBody] ReallocateOrderLineRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ReallocateOrderLineCommand
        {
            OrderId = orderId, BranchId = branchId, LineId = lineId, TargetBatchId = request.TargetBatchId, Reason = request.Reason ?? string.Empty,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }
}
