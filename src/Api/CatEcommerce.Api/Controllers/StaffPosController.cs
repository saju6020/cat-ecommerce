using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Order;
using CatEcommerce.Api.Idempotency;
using CatEcommerce.Api.Ordering;
using CatEcommerce.Application.CommandHandlers.Order;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-ORD-001 §12.4 (US-ORD-16, ADR-212) -- the point of sale: lookup, counter quote, sale, receipt. Everything is <c>Pos.Sell</c> and works in the staff member's <b>active
/// branch</b> (<c>X-Branch-Id</c>); a sale of another branch has no receipt for the caller (404). The sale is idempotent at the HTTP edge (<c>Idempotency-Key</c>, ADR-202) and
/// answers <c>201</c> with the real result -- the invoice number and the change to give back -- never <c>202</c>.
/// </summary>
[ApiController]
[Route("api/staff/pos")]
public sealed class StaffPosController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : StaffOrderControllerBase(activeBranchAccessor)
{
    [HttpGet("sku")]
    [RequireAction(Actions.Pos.Sell)]
    public async Task<IActionResult> BySku([FromQuery] string? barcode)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (string.IsNullOrWhiteSpace(barcode) || barcode.Length > 64) return BadRequest(new { error = "barcode is required (up to 64 characters)." });

        var response = await dispatcher.GetResultAsync(new GetPosSkuQuery { BranchId = branchId, Barcode = barcode, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("skus")]
    [RequireAction(Actions.Pos.Sell)]
    public async Task<IActionResult> Search([FromQuery] string? q)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2 || q.Length > 100) return BadRequest(new { error = "q must be 2 to 100 characters." });

        var response = await dispatcher.GetResultAsync(new SearchPosSkusQuery { BranchId = branchId, Text = q, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost("quote")]
    [RequireAction(Actions.Pos.Sell)]
    public async Task<IActionResult> Quote([FromBody] PosQuoteRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (request.Lines.Count is 0 or > OrderRules.MaxLines || request.Lines.Any(l => l.Quantity < 1 || l.Quantity > OrderRules.MaxQuantityPerLineHardCap))
            return BadRequest(new { error = $"lines must have 1 to {OrderRules.MaxLines} items, each with a quantity of at least 1." });

        var response = await dispatcher.GetResultAsync(new GetCounterQuoteQuery
        {
            BranchId = branchId, CustomerId = request.CustomerId, Lines = request.Lines.Select(l => new CounterQuoteLine(l.SkuId, l.Quantity, l.CatId)).ToList(),
            DiscountCode = request.DiscountCode, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }

    [HttpPost("sales")]
    [RequireAction(Actions.Pos.Sell)]
    [Idempotent]
    public async Task<IActionResult> Sell([FromBody] PosSaleRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var tenders = new List<CounterTenderInput>();
        foreach (var tender in request.Tenders)
        {
            if (!EnumQueryParameter.TryParse<TenderMethod>(tender.Method, out var method) || method is null) return BadRequest(new { error = "tender method must be Cash or InStoreCard." });
            tenders.Add(new CounterTenderInput(method, tender.Amount, tender.Reference));
        }

        var key = Guid.Parse(Request.Headers[IdempotencyRules.HeaderName].ToString());
        var response = await dispatcher.SendAsync(new CompleteCounterSaleCommand
        {
            OrderId = Guid.NewGuid(), IdempotencyKey = key, BranchId = branchId, CustomerName = request.CustomerName ?? string.Empty, CustomerMobile = request.CustomerMobile ?? string.Empty,
            Lines = request.Lines.Select(l => new CounterSaleLineInput(l.SkuId, l.Quantity, l.CatId)).ToList(), DiscountCode = request.DiscountCode, Tenders = tenders,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (!response.IsSuccess) return OrderingResults.Failure(this, response);

        var sale = (CounterSaleResult)response.Result;
        return StatusCode(201, new
        {
            orderId = sale.OrderId, orderNumber = sale.OrderNumber, invoiceNumber = sale.InvoiceNumber, grandTotal = OrderMoney.Of(sale.GrandTotal, sale.CurrencyCode),
            changeDue = OrderMoney.Of(sale.ChangeDue, sale.CurrencyCode),
        });
    }

    [HttpGet("sales/{orderId:guid}/receipt")]
    [RequireAction(Actions.Pos.Sell)]
    public async Task<IActionResult> Receipt(Guid orderId)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetReceiptQuery { BranchId = branchId, OrderId = orderId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
