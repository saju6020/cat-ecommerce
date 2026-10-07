using CatEcommerce.Api.Authorization;
using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Read.Queries.Notification;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-NTF-001 §12.4 -- US-NTF-01.</summary>
[ApiController]
[Route("api/customers/me/inbox")]
[RequireCustomerSession]
public sealed class CustomerInboxController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool? unreadOnly, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (pageNumber is > PagingRules.MaxPageNumber) return BadRequest(new { error = $"pageNumber cannot exceed {PagingRules.MaxPageNumber}." });
        if (pageSize is > PagingRules.MaxPageSize) return BadRequest(new { error = $"pageSize cannot exceed {PagingRules.MaxPageSize}." });

        var query = new GetInboxQuery
        {
            CustomerId = userContextProvider.GetUserContext().UserId, UnreadOnly = unreadOnly ?? false,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (pageNumber is > 0) query.PageNumber = pageNumber.Value;
        if (pageSize is > 0) query.PageSize = pageSize.Value;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var response = await dispatcher.GetResultAsync(new GetInboxUnreadCountQuery
        {
            CustomerId = userContextProvider.GetUserContext().UserId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });
        return Ok(new { count = response.Result });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var command = new MarkInboxItemReadCommand { NotificationId = id, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true };
        var response = await dispatcher.SendAsync(command);

        if (response.IsSuccess) return NoContent();

        var notFound = response.ValidationResult?.Errors.Any(e => e.ErrorCode == BusinessViolationCodes.NotificationNotFound) ?? false;
        return notFound ? NotFound() : BadRequest(response.ValidationResult);
    }
}
