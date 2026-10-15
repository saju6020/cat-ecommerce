using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Billing;
using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Billing.Documents;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-BIL-001 section 12.4 (US-BIL-05 AC2, ADR-186) -- the only way to correct an issued invoice: a credit note of its own, then
/// optionally a corrected draft linked to it. There is no route that edits an issued invoice. Staff, branch-scoped.
/// </summary>
[ApiController]
[Route("api/staff/billing")]
public sealed class StaffCreditNotesController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor, ICreditNotePdfRenderer pdfRenderer)
    : StaffBillingControllerBase(activeBranchAccessor)
{
    [HttpPost("invoices/{id:guid}/credit-notes")]
    [RequireAction(Actions.CreditNotes.Issue)]
    public async Task<IActionResult> Issue(Guid id, [FromBody] IssueCreditNoteRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;
        if (!TryReadIdempotencyKey(out var key, out var keyRefusal)) return keyRefusal!;

        var response = await dispatcher.SendAsync(new IssueCreditNoteCommand
        {
            CreditNoteId = Guid.NewGuid(), InvoiceId = id, BranchId = branchId, Kind = request.Kind, Reason = request.Reason, FullCredit = request.FullCredit,
            Lines = request.Lines, IdempotencyKey = key,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult, BusinessViolationCodes.InvoiceNotFound);

        var result = (IssuedCreditNoteResult)response.Result;
        var body = new { creditNoteId = result.CreditNoteId, number = result.Number, releasedToCustomerCredit = result.ReleasedToCustomerCredit };
        return result.Replayed ? Ok(body) : StatusCode(201, body);
    }

    [HttpPost("invoices/{id:guid}/corrected-draft")]
    [RequireAction(Actions.Billing.DraftInvoices)]
    public async Task<IActionResult> CreateCorrectedDraft(Guid id, [FromBody] CreateCorrectedDraftRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var invoiceId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new CreateCorrectedDraftCommand
        {
            NewInvoiceId = invoiceId, InvoiceId = id, CreditNoteId = request.CreditNoteId, BranchId = branchId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        return response.IsSuccess
            ? StatusCode(201, new { invoiceId })
            : RespondToFailure(response.ValidationResult, BusinessViolationCodes.InvoiceNotFound, BusinessViolationCodes.CreditNoteNotFound);
    }

    [HttpPut("invoices/{id:guid}/lines")]
    [RequireAction(Actions.Billing.DraftInvoices)]
    public async Task<IActionResult> SetCorrectedLines(Guid id, [FromBody] SetCorrectedDraftLinesRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new SetCorrectedDraftLinesCommand
        {
            InvoiceId = id, BranchId = branchId, ExpectedVersion = request.ExpectedVersion, Lines = request.Lines,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        return response.IsSuccess ? Ok() : RespondToFailure(response.ValidationResult, BusinessViolationCodes.InvoiceNotFound);
    }

    [HttpGet("credit-notes/{id:guid}")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetCreditNoteDetailQuery
        { CreditNoteId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("credit-notes/{id:guid}.pdf")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> GetPdf(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetCreditNoteDocumentQuery
        { CreditNoteId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        if (response.Result is null) return NotFound();

        Response.Headers.CacheControl = "no-store";
        return File(pdfRenderer.Render(response.Result), "application/pdf", $"{response.Result.Number}.pdf");
    }
}
