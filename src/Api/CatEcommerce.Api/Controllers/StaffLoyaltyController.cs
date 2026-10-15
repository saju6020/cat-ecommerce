using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-BIL-001 section 12.4 (US-BIL-10, ADR-191) -- loyalty points: redeeming them on a draft invoice and reading a customer ledger. Staff, branch-scoped.
/// Redemption is refused (<c>LoyaltyNotEnabled</c>) unless <c>billing.loyaltyEnabled</c> is on.
/// </summary>
[ApiController]
[Route("api/staff/billing")]
public sealed class StaffLoyaltyController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : StaffBillingControllerBase(activeBranchAccessor)
{
    [HttpPost("invoices/{id:guid}/loyalty-redemption")]
    [RequireAction(Actions.Billing.DraftInvoices)]
    public async Task<IActionResult> Redeem(Guid id, [FromBody] ApplyLoyaltyRedemptionRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new ApplyLoyaltyRedemptionCommand
        {
            InvoiceId = id, BranchId = branchId, Points = request.Points,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        return response.IsSuccess ? await InvoiceAsync(id, branchId) : RespondToFailure(response.ValidationResult, BusinessViolationCodes.InvoiceNotFound);
    }

    [HttpDelete("invoices/{id:guid}/loyalty-redemption")]
    [RequireAction(Actions.Billing.DraftInvoices)]
    public async Task<IActionResult> RemoveRedemption(Guid id)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RemoveLoyaltyRedemptionCommand
        {
            InvoiceId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        return response.IsSuccess ? await InvoiceAsync(id, branchId) : RespondToFailure(response.ValidationResult, BusinessViolationCodes.InvoiceNotFound);
    }

    [HttpGet("customers/{customerId:guid}/loyalty")]
    [RequireAction(Actions.Billing.View)]
    public async Task<IActionResult> Ledger(Guid customerId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        var response = await dispatcher.GetResultAsync(new GetLoyaltyQuery
        { CustomerId = customerId, PageNumber = pageNumber, PageSize = pageSize, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    private async Task<IActionResult> InvoiceAsync(Guid id, Guid branchId)
    {
        var invoice = await dispatcher.GetResultAsync(new GetInvoiceDetailQuery
        { InvoiceId = id, BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return invoice.Result is null ? NotFound() : Ok(new { invoice = invoice.Result });
    }
}
