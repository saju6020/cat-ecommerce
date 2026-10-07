using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Notification;
using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Read.Queries.Notification;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-NTF-001 §12.4 -- US-NTF-06.</summary>
[ApiController]
[Route("api/admin/notifications")]
public sealed class AdminNotificationsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.NotificationLog.View)]
    public async Task<IActionResult> Get(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] Guid? recipientId,
        [FromQuery] string? destination, [FromQuery] string? channel, [FromQuery] string? templateKey, [FromQuery] string? status,
        [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        if (from is null || to is null) return BadRequest(new { error = "A date range (from, to) is required." });
        if (to.Value < from.Value) return BadRequest(new { error = "'to' cannot be before 'from'." });
        if ((to.Value - from.Value).TotalDays > NotificationLogRules.MaxRangeDays)
            return BadRequest(new { error = $"The date range cannot exceed {NotificationLogRules.MaxRangeDays} days." });
        if (ControlCharacterGuard.ContainsDisallowedControlCharacter(destination) || ControlCharacterGuard.ContainsDisallowedControlCharacter(templateKey))
            return BadRequest(new { error = "destination and templateKey must not contain control characters." });
        if (pageNumber is > PagingRules.MaxPageNumber) return BadRequest(new { error = $"pageNumber cannot exceed {PagingRules.MaxPageNumber}." });
        if (pageSize is > PagingRules.MaxPageSize) return BadRequest(new { error = $"pageSize cannot exceed {PagingRules.MaxPageSize}." });

        var normalisedDestination = NormaliseDestination(destination);

        var query = new GetNotificationLogQuery
        {
            From = from.Value, To = to.Value, RecipientId = recipientId, Destination = normalisedDestination,
            Channel = channel, TemplateKey = templateKey, Status = status,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (pageNumber is > 0) query.PageNumber = pageNumber.Value;
        if (pageSize is > 0) query.PageSize = pageSize.Value;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("summary")]
    [RequireAction(Actions.NotificationLog.View)]
    public async Task<IActionResult> Summary()
    {
        var response = await dispatcher.GetResultAsync(new GetNotificationSummaryQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpGet("{id:guid}")]
    [RequireAction(Actions.NotificationLog.View)]
    public async Task<IActionResult> GetOne(Guid id)
    {
        var response = await dispatcher.GetResultAsync(new GetNotificationDetailQuery { NotificationId = id, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost("{id:guid}/resend")]
    [RequireAction(Actions.NotificationLog.Resend)]
    public async Task<IActionResult> Resend(Guid id, [FromBody] ResendNotificationRequest request)
    {
        var newId = Guid.NewGuid();
        var command = new ResendNotificationCommand
        {
            NotificationId = id, NewNotificationId = newId, Channel = request.Channel,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        if (!response.IsSuccess) return BadRequest(response.ValidationResult);

        // Gate fix 2026-09-27 (ARCHITECTURE LOW): §12.4 specifies { notificationId, status, notBefore? }.
        var result = (ResendNotificationResult)response.Result!;
        return StatusCode(201, new { notificationId = result.NotificationId, status = result.Status, notBefore = result.NotBefore });
    }

    /// <summary>Normalises a caller-typed destination the same way the stored value is
    /// normalised (mobile via MobileNumber, email lower-cased) -- an un-normalised search would
    /// otherwise never match. Falls back to the raw value on a malformed input so the search still
    /// runs (and simply finds nothing).</summary>
    private static string? NormaliseDestination(string? destination)
    {
        if (string.IsNullOrWhiteSpace(destination)) return null;

        try
        {
            return CatEcommerce.Domain.ValueObjects.MobileNumber.Create(destination).Value;
        }
        catch (CatEcommerce.Domain.Exceptions.DomainRuleViolationException)
        {
            return destination.Contains('@') ? destination.Trim().ToLowerInvariant() : destination;
        }
    }
}
