using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Product;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-SHP-001 section 12.4 -- customer's own reviews and review eligibility. Every
/// handler resolves by id and caller: another customer's review is 404 (ReviewNotFound).</summary>
[ApiController]
[Route("api/customers/me")]
[RequireCustomerSession]
[StorefrontEndpoint]
public sealed class CustomerReviewsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet("shop/products/{slug}/review-eligibility")]
    public async Task<IActionResult> GetReviewEligibility(string slug)
    {
        var customerId = userContextProvider.GetUserContext().UserId;
        var response = await dispatcher.GetResultAsync(new GetReviewEligibilityQuery
        { CustomerId = customerId, Slug = slug, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("reviews")]
    public async Task<IActionResult> GetMyReviews()
    {
        var customerId = userContextProvider.GetUserContext().UserId;
        var response = await dispatcher.GetResultAsync(new GetMyReviewsQuery
        { CustomerId = customerId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    [HttpPost("reviews")]
    [EnableRateLimiting(ShopRateLimitPolicies.Reviews)]
    public async Task<IActionResult> Submit([FromBody] SubmitProductReviewRequest request)
    {
        var reviewId = Guid.NewGuid();
        var command = new SubmitProductReviewCommand
        {
            ReviewId = reviewId, ProductId = request.ProductId, SkuId = request.SkuId, Rating = request.Rating,
            Title = request.Title, Body = request.Body, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { reviewId, status = "PendingModeration" }) : BadRequest(response.ValidationResult);
    }

    [HttpPut("reviews/{reviewId:guid}")]
    [EnableRateLimiting(ShopRateLimitPolicies.Reviews)]
    public async Task<IActionResult> Edit(Guid reviewId, [FromBody] EditProductReviewRequest request)
    {
        var command = new EditProductReviewCommand
        {
            ReviewId = reviewId, Rating = request.Rating, Title = request.Title, Body = request.Body,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        if (response.IsSuccess) return Ok(new { status = "PendingModeration" });

        var notFound = response.ValidationResult?.Errors.Any(e => e.ErrorCode == BusinessViolationCodes.ReviewNotFound) ?? false;
        return notFound ? NotFound() : BadRequest(response.ValidationResult);
    }

    [HttpDelete("reviews/{reviewId:guid}")]
    public async Task<IActionResult> Withdraw(Guid reviewId)
    {
        var command = new WithdrawProductReviewCommand
        { ReviewId = reviewId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true };

        var response = await dispatcher.SendAsync(command);
        if (response.IsSuccess) return NoContent();

        var notFound = response.ValidationResult?.Errors.Any(e => e.ErrorCode == BusinessViolationCodes.ReviewNotFound) ?? false;
        return notFound ? NotFound() : BadRequest(response.ValidationResult);
    }
}
