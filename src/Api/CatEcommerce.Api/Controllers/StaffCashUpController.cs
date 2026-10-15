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

/// <summary>HO-BIL-001 section 12.4 (US-BIL-12, US-BIL-06 AC3) -- the daily till count. Staff, branch-scoped. Counting is for reception,
/// accountants and administrators; <b>closing</b> the day is for accountants and administrators (OQ-12).</summary>
[ApiController]
[Route("api/staff/billing/cash-ups")]
public sealed class StaffCashUpController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : StaffBillingControllerBase(activeBranchAccessor)
{
    [HttpGet("{date}")]
    [RequireAction(Actions.CashUp.Count)]
    public async Task<IActionResult> Get(DateOnly date)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.GetResultAsync(new GetCashUpQuery
        { BranchId = branchId, BusinessDate = date, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    [HttpPut("{date}/count")]
    [RequireAction(Actions.CashUp.Count)]
    public async Task<IActionResult> Count(DateOnly date, [FromBody] RecordCashCountRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new RecordCashCountCommand
        {
            BranchId = branchId, BusinessDate = date, CountedCash = request.CountedCash, Explanation = request.Explanation,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        if (!response.IsSuccess) return RespondToFailure(response.ValidationResult);

        var view = await dispatcher.GetResultAsync(new GetCashUpQuery
        { BranchId = branchId, BusinessDate = date, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(view.Result);
    }

    [HttpPost("{date}/close")]
    [RequireAction(Actions.CashUp.Close)]
    public async Task<IActionResult> Close(DateOnly date, [FromBody] CloseCashUpRequest request)
    {
        if (!TryResolveBranch(out var branchId, out var refusal)) return refusal!;

        var response = await dispatcher.SendAsync(new CloseCashUpCommand
        {
            BranchId = branchId, BusinessDate = date, ExpectedVersion = request.ExpectedVersion,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        return response.IsSuccess ? NoContent() : RespondToFailure(response.ValidationResult);
    }
}
