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

/// <summary>HO-ORD-001 §12.4 (US-ORD-05) -- discount codes: percentage or fixed, minimum spend, product or category scope, first-order-only, overall and
/// per-customer limits and a validity window. The list carries <c>timesRedeemed</c>. A code's text cannot be edited.</summary>
[ApiController]
[Route("api/admin/discount-codes")]
public sealed class AdminDiscountCodesController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Discounts.View)]
    public async Task<IActionResult> List()
    {
        var response = await dispatcher.GetResultAsync(new GetDiscountCodesQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Discounts.Manage)]
    public async Task<IActionResult> Create([FromBody] CreateDiscountCodeRequest request)
    {
        var discountCodeId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new CreateDiscountCodeCommand
        {
            DiscountCodeId = discountCodeId, Code = request.Code, Description = request.Description, Kind = request.Kind, Percent = request.Percent,
            Amount = request.Amount, MinimumSpend = request.MinimumSpend, Scope = request.Scope, ProductIds = request.ProductIds, CategoryIds = request.CategoryIds,
            FirstOrderOnly = request.FirstOrderOnly, UsageLimit = request.UsageLimit, PerCustomerLimit = request.PerCustomerLimit,
            ValidFrom = request.ValidFrom, ValidTo = request.ValidTo, IsActive = request.IsActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? StatusCode(201, new { discountCodeId }) : OrderingResults.Failure(this, response);
    }

    [HttpPut("{discountCodeId:guid}")]
    [RequireAction(Actions.Discounts.Manage)]
    public async Task<IActionResult> Update(Guid discountCodeId, [FromBody] UpdateDiscountCodeRequest request)
    {
        var response = await dispatcher.SendAsync(new UpdateDiscountCodeCommand
        {
            DiscountCodeId = discountCodeId, Description = request.Description, Kind = request.Kind, Percent = request.Percent, Amount = request.Amount,
            MinimumSpend = request.MinimumSpend, Scope = request.Scope, ProductIds = request.ProductIds, CategoryIds = request.CategoryIds,
            FirstOrderOnly = request.FirstOrderOnly, UsageLimit = request.UsageLimit, PerCustomerLimit = request.PerCustomerLimit,
            ValidFrom = request.ValidFrom, ValidTo = request.ValidTo, IsActive = request.IsActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }

    [HttpPut("{discountCodeId:guid}/activation")]
    [RequireAction(Actions.Discounts.Manage)]
    public async Task<IActionResult> SetActivation(Guid discountCodeId, [FromBody] SetActivationRequest request)
    {
        var response = await dispatcher.SendAsync(new SetDiscountCodeActivationCommand
        {
            DiscountCodeId = discountCodeId, IsActive = request.IsActive, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? NoContent() : OrderingResults.Failure(this, response);
    }
}
