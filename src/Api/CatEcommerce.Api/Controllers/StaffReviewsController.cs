using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Product;
using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-SHP-001 section 12.4 -- review moderation queue.</summary>
[ApiController]
[Route("api/staff/shop/reviews")]
public sealed class StaffReviewsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Shop.ReviewsModerate)]
    public async Task<IActionResult> Get([FromQuery] string? status, [FromQuery] Guid? productId, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        var query = new GetReviewModerationQueueQuery { Status = status, ProductId = productId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() };

        var pagingError = PagingHelper.ApplyPaging(query, pageNumber, pageSize);
        if (pagingError is not null) return pagingError;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpPost("{reviewId:guid}/publish")]
    [RequireAction(Actions.Shop.ReviewsModerate)]
    public async Task<IActionResult> Publish(Guid reviewId)
    {
        var response = await dispatcher.SendAsync(new PublishProductReviewCommand
        { ReviewId = reviewId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpPost("{reviewId:guid}/reject")]
    [RequireAction(Actions.Shop.ReviewsModerate)]
    public async Task<IActionResult> Reject(Guid reviewId, [FromBody] RejectProductReviewRequest request)
    {
        var response = await dispatcher.SendAsync(new RejectProductReviewCommand
        { ReviewId = reviewId, Reason = request.Reason, Note = request.Note, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpPost("{reviewId:guid}/hide")]
    [RequireAction(Actions.Shop.ReviewsModerate)]
    public async Task<IActionResult> Hide(Guid reviewId)
    {
        var response = await dispatcher.SendAsync(new HideProductReviewCommand
        { ReviewId = reviewId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }
}
