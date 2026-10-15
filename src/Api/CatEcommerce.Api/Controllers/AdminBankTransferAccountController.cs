using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Order;
using CatEcommerce.Api.Ordering;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ORD-001 §12.4 / §20.8 (US-ORD-19) -- the shop's one bank-transfer account. Administrator only (<c>Orders.ManagePaymentSettings</c>).
/// It is data the administrator enters, never configuration, so the number is not in a committed file.</summary>
[ApiController]
[Route("api/admin/bank-transfer-account")]
public sealed class AdminBankTransferAccountController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Orders.ManagePaymentSettings)]
    public async Task<IActionResult> Get()
    {
        Response.Headers.CacheControl = "no-store";
        var response = await dispatcher.GetResultAsync(new GetBankTransferAccountQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPut]
    [RequireAction(Actions.Orders.ManagePaymentSettings)]
    public async Task<IActionResult> Set([FromBody] BankTransferAccountRequest request)
    {
        var response = await dispatcher.SendAsync(new SetBankTransferAccountCommand
        {
            AccountName = request.AccountName, AccountNumber = request.AccountNumber, BankName = request.BankName, BranchName = request.BranchName,
            RoutingNumber = request.RoutingNumber, IsActive = request.IsActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }
}
