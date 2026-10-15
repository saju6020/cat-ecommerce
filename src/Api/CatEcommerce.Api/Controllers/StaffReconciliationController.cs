using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Billing;
using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-BIL-001 section 12.4 (US-BIL-06) -- reconciling money received against what couriers handed over and what the bank shows. Accountants and
/// administrators, branch-scoped. A statement file is parsed in memory and never stored; it is refused above 1 MiB (<c>413</c>) and
/// when any row is malformed (<c>400</c> naming the first rows, never their content).
/// </summary>
[ApiController]
[Route("api/staff/billing")]
public sealed class StaffReconciliationController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor, IBankStatementCsvParser csvParser)
    : StaffBillingControllerBase(activeBranchAccessor)
{
    private const int MaxReportedRows = 10;

    // ── COD ─────────────────────────────────────────────────────────────────────────────────────────
    [HttpGet("cod/collections")]
    [RequireAction(Actions.Reconciliation.Manage)]
    public async Task<IActionResult> Collections(
        [FromQuery] string? courierCode, [FromQuery] string? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetCodCollectionsQuery
        {
            BranchId = branchId, CourierCode = courierCode, Status = status, From = from, To = to, PageNumber = pageNumber, PageSize = pageSize,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
        });

        return Ok(response.Result);
    }

    [HttpPost("cod/remittances")]
    [RequireAction(Actions.Reconciliation.Manage)]
    public async Task<IActionResult> RecordRemittance([FromBody] RecordCodRemittanceRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RecordCodRemittanceCommand
        {
            RemittanceId = Guid.NewGuid(), BranchId = branchId, CourierCode = request.CourierCode, ReceivedOn = request.ReceivedOn, Amount = request.Amount,
            Method = request.Method, Reference = request.Reference, Lines = request.Lines,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult, BusinessViolationCodes.PaymentNotFound);

        var result = (RecordedRemittanceResult)response.Result;
        return StatusCode(201, new { remittanceId = result.RemittanceId, unapplied = result.Unapplied });
    }

    [HttpGet("cod/remittances")]
    [RequireAction(Actions.Reconciliation.Manage)]
    public async Task<IActionResult> Remittances([FromQuery] string? courierCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetCodRemittancesQuery
        {
            BranchId = branchId, CourierCode = courierCode, PageNumber = pageNumber, PageSize = pageSize,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
        });

        return Ok(response.Result);
    }

    [HttpGet("cod/reconciliation")]
    [RequireAction(Actions.Reconciliation.Manage)]
    public async Task<IActionResult> CodReconciliation([FromQuery] string? courierCode, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetCodReconciliationQuery
        {
            BranchId = branchId, CourierCode = courierCode, From = from, To = to,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
        });

        return Ok(response.Result);
    }

    // ── Bank statements ─────────────────────────────────────────────────────────────────────────────
    [HttpPost("bank-statements")]
    [RequireAction(Actions.Reconciliation.Manage)]
    [RequestSizeLimit(2 * BillingRules.MaxStatementBytes)]
    public async Task<IActionResult> Import([FromForm] ImportBankStatementRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        if (request.File is null || request.File.Length == 0)
            return Refuse(400, BusinessViolationCodes.StatementMalformed, "Choose the statement file (a .csv).");

        if (request.File.Length > BillingRules.MaxStatementBytes)
            return Refuse(413, BusinessViolationCodes.StatementTooLarge, "The statement file is larger than 1 MiB.");

        byte[] bytes;
        await using (var stream = request.File.OpenReadStream())
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            bytes = buffer.ToArray();
        }

        var parsed = csvParser.Parse(bytes);
        if (parsed.TooLarge) return Refuse(413, BusinessViolationCodes.StatementTooLarge, "The statement is too large to import.");

        if (!parsed.IsValid)
        {
            var validation = new ValidationResponse();
            foreach (var error in parsed.Errors.Take(MaxReportedRows))
                validation.AddError(errorMessage: $"Row {error.RowNumber}: {error.Message}", propertyName: "file", errorCode: BusinessViolationCodes.StatementMalformed, resourceName: "Billing");

            return BadRequest(validation);
        }

        var statementId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new ImportBankStatementCommand
        {
            StatementId = statementId, BranchId = branchId, BankAccountLabel = request.BankAccountLabel, PeriodFrom = request.PeriodFrom, PeriodTo = request.PeriodTo,
            SourceFileName = Path.GetFileName(request.File.FileName), SourceSha256 = parsed.Sha256,
            Lines = parsed.Lines.Select(l => new StatementLineInput(l.LineNumber, l.TransactionDate, l.Amount, l.Direction.DisplayName, l.Reference, l.Description, l.BankTransactionId)).ToList(),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult);

        var result = (ImportedStatementResult)response.Result;
        return StatusCode(201, new { statementId = result.StatementId, lineCount = result.LineCount });
    }

    [HttpGet("bank-statements")]
    [RequireAction(Actions.Reconciliation.Manage)]
    public async Task<IActionResult> Statements([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetBankStatementsQuery
        {
            BranchId = branchId, PageNumber = pageNumber, PageSize = pageSize, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
        });

        return Ok(response.Result);
    }

    [HttpGet("bank-statements/{id:guid}/reconciliation")]
    [RequireAction(Actions.Reconciliation.Manage)]
    public async Task<IActionResult> StatementReconciliation(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetBankStatementReconciliationQuery
        { BranchId = branchId, StatementId = id, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("bank-statements/{id:guid}/auto-match")]
    [RequireAction(Actions.Reconciliation.Manage)]
    public async Task<IActionResult> AutoMatch(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new AutoMatchBankStatementCommand
        {
            StatementId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult, BusinessViolationCodes.StatementNotFound);

        var result = (AutoMatchResult)response.Result;
        return Ok(new { matched = result.Matched, ambiguous = result.Ambiguous, none = result.None });
    }

    [HttpPost("bank-matches")]
    [RequireAction(Actions.Reconciliation.Manage)]
    public async Task<IActionResult> Match([FromBody] MatchBankLineRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var matchId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new MatchBankLineCommand
        {
            MatchId = matchId, StatementId = request.StatementId, StatementLineId = request.StatementLineId, PaymentId = request.PaymentId, BranchId = branchId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        return response.IsSuccess
            ? StatusCode(201, new { matchId })
            : RespondToFailure(response.ValidationResult, BusinessViolationCodes.StatementNotFound, BusinessViolationCodes.PaymentNotFound);
    }

    [HttpPost("bank-matches/{id:guid}/unmatch")]
    [RequireAction(Actions.Reconciliation.Manage)]
    public async Task<IActionResult> Unmatch(Guid id, [FromBody] UnmatchBankLineRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new UnmatchBankLineCommand
        {
            MatchId = id, BranchId = branchId, Reason = request.Reason,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        return response.IsSuccess ? NoContent() : RespondToFailure(response.ValidationResult, BusinessViolationCodes.BankMatchNotFound);
    }

    private IActionResult Refuse(int status, string code, string message)
    {
        var validation = new ValidationResponse();
        validation.AddError(errorMessage: message, propertyName: "file", errorCode: code, resourceName: "Billing");
        return StatusCode(status, validation);
    }
}
