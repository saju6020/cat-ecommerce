using CatEcommerce.Api.Authorization;
using CatEcommerce.Infrastructure.Billing.Documents;
using CatEcommerce.Read.Queries.Billing;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-BIL-001 §12.4 "Customer (own)". Every invoice and payment is resolved <b>by id and caller</b> -- the caller or a
/// customer merged into them (ADR-192); someone else's is <c>404</c>, never <c>403</c>. Drafts, discarded invoices, and
/// pending or rejected payments are never visible to a customer (§12.3).
/// </summary>
[ApiController]
[Route("api/customers/me")]
[RequireCustomerSession]
public sealed class CustomerBillingController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IInvoicePdfRenderer pdfRenderer) : ControllerBase
{
    private Guid CallerId => userContextProvider.GetUserContext().UserId;

    [HttpGet("loyalty")]
    public async Task<IActionResult> GetLoyalty([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var response = await dispatcher.GetResultAsync(new GetLoyaltyQuery
        { CustomerId = CallerId, PageNumber = pageNumber, PageSize = pageSize, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var response = await dispatcher.GetResultAsync(new GetCustomerInvoicesQuery
        {
            CustomerId = CallerId, PageNumber = pageNumber, PageSize = pageSize, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
        });

        return Ok(response.Result);
    }

    [HttpGet("invoices/{id:guid}")]
    public async Task<IActionResult> GetInvoice(Guid id)
    {
        var response = await dispatcher.GetResultAsync(new GetCustomerInvoiceDetailQuery
        { CustomerId = CallerId, InvoiceId = id, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("invoices/{id:guid}.pdf")]
    public async Task<IActionResult> GetInvoicePdf(Guid id)
    {
        var response = await dispatcher.GetResultAsync(new GetInvoiceDocumentQuery
        { InvoiceId = id, CustomerId = CallerId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        if (response.Result is null) return NotFound();

        Response.Headers.CacheControl = "no-store";
        return File(pdfRenderer.Render(response.Result), "application/pdf", $"{response.Result.Number}.pdf");
    }

    /// <summary>HO-POR-001's input -- the caller's own money position.</summary>
    [HttpGet("billing/summary")]
    public async Task<IActionResult> GetSummary()
    {
        var response = await dispatcher.GetResultAsync(new GetCustomerBillingSummaryQuery
        { CustomerId = CallerId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    /// <summary>Accepted and reversed payments only -- a pending or rejected one is never visible to a customer.</summary>
    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var response = await dispatcher.GetResultAsync(new GetCustomerPaymentsQuery
        {
            CustomerId = CallerId, PageNumber = pageNumber, PageSize = pageSize, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
        });

        return Ok(response.Result);
    }

    /// <summary>The receipt data of one of the caller's own payments; someone else's is <c>404</c>.</summary>
    [HttpGet("payments/{id:guid}")]
    public async Task<IActionResult> GetPayment(Guid id)
    {
        var response = await dispatcher.GetResultAsync(new GetPaymentReceiptQuery
        { PaymentId = id, CustomerId = CallerId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
