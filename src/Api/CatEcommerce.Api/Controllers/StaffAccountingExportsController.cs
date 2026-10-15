using CatEcommerce.Api.Authorization;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Billing.Export;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-BIL-001 section 12.4 (US-BIL-09, ADR-190) -- the accounting export, on demand: the immutable facts of a date range as a deterministic ZIP of CSVs.
/// The same range yields the same bytes; every run is recorded (who, range, counts, SHA-256) by a command of its own. Accountants and
/// administrators; the branch is optional (absent means every branch).
/// </summary>
[ApiController]
[Route("api/staff/billing/accounting-exports")]
public sealed class StaffAccountingExportsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.AccountingExport.Run)]
    public async Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        var response = await dispatcher.GetResultAsync(new GetAccountingExportRunsQuery
        { PageNumber = pageNumber, PageSize = pageSize, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    [HttpGet("download")]
    [RequireAction(Actions.AccountingExport.Run)]
    public async Task<IActionResult> Download([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? branchId)
    {
        if (from > to || to.DayNumber - from.DayNumber + 1 > BillingRules.MaxExportRangeDays)
            return Refuse($"An export can cover at most {BillingRules.MaxExportRangeDays} days.");

        var content = (await dispatcher.GetResultAsync(new GetAccountingExportContentQuery
        { From = from, To = to, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() })).Result;

        if (content.TotalRows > BillingRules.MaxExportRows)
            return Refuse($"This range holds more than {BillingRules.MaxExportRows} rows; choose a shorter range.");

        var file = AccountingExportWriter.Write(content);

        var recorded = await dispatcher.SendAsync(new RecordAccountingExportRunCommand
        {
            RunId = Guid.NewGuid(), From = from, To = to, BranchId = branchId, InvoiceCount = file.InvoiceCount, InvoiceLineCount = file.InvoiceLineCount,
            PaymentCount = file.PaymentCount, AllocationCount = file.AllocationCount, CreditNoteCount = file.CreditNoteCount, RefundCount = file.RefundCount, Sha256 = file.Sha256,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        if (!recorded.IsSuccess) return BadRequest(recorded.ValidationResult);

        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Export-Sha256"] = file.Sha256;
        return File(file.Bytes, "application/zip", $"purrcare-accounts-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}.zip");
    }

    private IActionResult Refuse(string message)
    {
        var validation = new ValidationResponse();
        validation.AddError(errorMessage: message, propertyName: "to", errorCode: BusinessViolationCodes.ExportRangeTooLarge, resourceName: "Billing");
        return BadRequest(validation);
    }
}
