using System.Text;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Billing;
using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Billing;
using CatEcommerce.Infrastructure.Billing.Documents;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-BIL-001 §12.4 -- charges become sequentially numbered, itemised, tax-correct invoices (US-BIL-01, 02, 05, 07).
/// Staff, branch-scoped (<c>X-Branch-Id</c>); an invoice of another branch is "not found". The money-moving writes accept an
/// <c>Idempotency-Key</c> (ADR-187). Corrections are credit notes (<see cref="StaffCreditNotesController"/>), never edits.
/// </summary>
[ApiController]
[Route("api/staff/billing/invoices")]
public sealed class StaffInvoicesController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor, IInvoicePdfRenderer pdfRenderer)
    : StaffBillingControllerBase(activeBranchAccessor)
{
    [HttpPost("preview")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> Preview([FromBody] PreviewInvoiceRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new PreviewInvoiceQuery
        {
            CustomerId = request.CustomerId, BranchId = branchId, ChargeIds = request.ChargeIds, LoyaltyPoints = request.LoyaltyPoints,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
        });

        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Billing.DraftInvoices)]
    public async Task<IActionResult> CreateDraft([FromBody] CreateDraftInvoiceRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var invoiceId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new CreateDraftInvoiceCommand
        {
            InvoiceId = invoiceId, CustomerId = request.CustomerId, BranchId = branchId, ChargeIds = request.ChargeIds, Notes = request.Notes,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        return response.IsSuccess ? StatusCode(201, new { invoiceId }) : RespondToFailure(response.ValidationResult);
    }

    [HttpPut("{id:guid}/charges")]
    [RequireAction(Actions.Billing.DraftInvoices)]
    public async Task<IActionResult> SetCharges(Guid id, [FromBody] SetDraftInvoiceChargesRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new SetDraftInvoiceChargesCommand
        {
            InvoiceId = id, BranchId = branchId, ExpectedVersion = request.ExpectedVersion, ChargeIds = request.ChargeIds,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult, BusinessViolationCodes.InvoiceNotFound);

        return await DetailAsync(id, branchId);
    }

    [HttpPost("{id:guid}/discard")]
    [RequireAction(Actions.Billing.DraftInvoices)]
    public async Task<IActionResult> Discard(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new DiscardDraftInvoiceCommand
        {
            InvoiceId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        return response.IsSuccess ? NoContent() : RespondToFailure(response.ValidationResult, BusinessViolationCodes.InvoiceNotFound);
    }

    [HttpPost("{id:guid}/issue")]
    [RequireAction(Actions.Billing.IssueInvoices)]
    public async Task<IActionResult> Issue(Guid id, [FromBody] IssueInvoiceRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (!TryReadIdempotencyKey(out var key, out var keyRefusal)) return keyRefusal!;

        var response = await dispatcher.SendAsync(new IssueInvoiceCommand
        {
            InvoiceId = id, BranchId = branchId, ExpectedVersion = request.ExpectedVersion, IdempotencyKey = key,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult, BusinessViolationCodes.InvoiceNotFound);

        var result = (IssuedInvoiceResult)response.Result;
        return Ok(new { invoiceId = result.InvoiceId, number = result.Number });
    }

    [HttpPost("issue-from-charges")]
    [RequireAction(Actions.Billing.IssueInvoices)]
    public async Task<IActionResult> IssueFromCharges([FromBody] IssueInvoiceFromChargesRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (!TryReadIdempotencyKey(out var key, out var keyRefusal)) return keyRefusal!;

        var response = await dispatcher.SendAsync(new IssueInvoiceFromChargesCommand
        {
            InvoiceId = Guid.NewGuid(), CustomerId = request.CustomerId, BranchId = branchId, Selector = request.Selector.ToSelector(), IdempotencyKey = key,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult);

        var result = (IssuedInvoiceResult)response.Result;
        var body = new { invoiceId = result.InvoiceId, number = result.Number };
        return result.Replayed ? Ok(body) : StatusCode(201, body);
    }

    [HttpGet]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? customerId, [FromQuery] string? status, [FromQuery] string? number, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? businessLine, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetInvoicesQuery
        {
            BranchId = branchId, CustomerId = customerId, Status = status, Number = number, From = from, To = to, BusinessLine = businessLine,
            PageNumber = pageNumber, PageSize = pageSize, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
        });

        return Ok(response.Result);
    }

    /// <summary>UI-4 -- the list as a CSV (injection-guarded: a text cell beginning <c>= + - @</c> is prefixed with <c>'</c>).</summary>
    [HttpGet("export.csv")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] Guid? customerId, [FromQuery] string? status, [FromQuery] string? number, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? businessLine)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetInvoicesQuery
        {
            BranchId = branchId, CustomerId = customerId, Status = status, Number = number, From = from, To = to, BusinessLine = businessLine, ForExport = true,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
        });

        var csv = new StringBuilder();
        BillingCsv.Row(csv, "invoiceId", "number", "status", "customerId", "customerName", "issueDate", "dueOn", "currency", "total", "paid", "credited", "outstanding");
        foreach (var i in response.Result.Items)
            BillingCsv.Row(csv, BillingCsv.Text(i.InvoiceId.ToString()), BillingCsv.Text(i.Number), BillingCsv.Text(i.Status), BillingCsv.Text(i.CustomerId.ToString()),
                BillingCsv.Text(i.CustomerName), BillingCsv.Text(BillingCsv.Date(i.IssueDate)), BillingCsv.Text(BillingCsv.Date(i.DueOn)), BillingCsv.Text(i.Total.CurrencyCode),
                i.Total.Amount, i.Paid.Amount, i.Credited.Amount, i.Outstanding.Amount);

        Response.Headers.CacheControl = "no-store";
        return File(new UTF8Encoding(false).GetBytes(csv.ToString()), "text/csv; charset=utf-8", "invoices.csv");
    }

    [HttpGet("{id:guid}")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        return await DetailAsync(id, branchId);
    }

    [HttpGet("{id:guid}.pdf")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> GetPdf(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetInvoiceDocumentQuery
        { InvoiceId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        if (response.Result is null) return NotFound();

        Response.Headers.CacheControl = "no-store";
        return File(pdfRenderer.Render(response.Result), "application/pdf", $"{response.Result.Number}.pdf");
    }

    private async Task<IActionResult> DetailAsync(Guid id, Guid branchId)
    {
        var response = await dispatcher.GetResultAsync(new GetInvoiceDetailQuery
        { InvoiceId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }
}
