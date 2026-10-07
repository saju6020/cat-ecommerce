using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Notification;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Read.Queries.Notification;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-NTF-001 §12.4 -- US-NTF-02.</summary>
[ApiController]
[Route("api/admin/notification-templates")]
public sealed class AdminNotificationTemplatesController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.NotificationTemplates.View)]
    public async Task<IActionResult> Get()
    {
        var response = await dispatcher.GetResultAsync(new GetNotificationTemplatesQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpGet("{key}")]
    [RequireAction(Actions.NotificationTemplates.View)]
    public async Task<IActionResult> GetOne(string key)
    {
        var response = await dispatcher.GetResultAsync(new GetNotificationTemplateQuery { Key = key, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPut("{key}")]
    [RequireAction(Actions.NotificationTemplates.Manage)]
    public async Task<IActionResult> Save(string key, [FromBody] SaveNotificationTemplateRequest request)
    {
        var command = new SaveNotificationTemplateCommand
        {
            Key = key,
            ExpectedContentVersion = request.ExpectedContentVersion,
            // Gate fix 2026-09-27 (SECURITY LOW): a `contents:[null]` body used to NPE right here,
            // before the command's own validator/handler ever ran. Null entries are now carried
            // through -- SaveNotificationTemplateCommandValidator/Handler reject them as 400.
            Contents = request.Contents.Select(c => c is null ? null! : new NotificationTemplateContentItem
            {
                Channel = c.Channel, Subject = c.Subject, Title = c.Title, Body = c.Body, LinkPath = c.LinkPath
            }).ToList(),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        if (response.IsSuccess) return NoContent();

        var isConflict = response.ValidationResult?.Errors?.Any(e => e.ErrorCode == BusinessViolationCodes.TemplateVersionConflict) ?? false;
        return isConflict ? Conflict(response.ValidationResult) : BadRequest(response.ValidationResult);
    }

    [HttpPost("{key}/reset")]
    [RequireAction(Actions.NotificationTemplates.Manage)]
    public async Task<IActionResult> Reset(string key, [FromBody] ResetNotificationTemplateRequest request)
    {
        var command = new ResetNotificationTemplateCommand
        {
            Key = key, ExpectedContentVersion = request.ExpectedContentVersion,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        if (response.IsSuccess) return NoContent();

        var isConflict = response.ValidationResult?.Errors?.Any(e => e.ErrorCode == BusinessViolationCodes.TemplateVersionConflict) ?? false;
        return isConflict ? Conflict(response.ValidationResult) : BadRequest(response.ValidationResult);
    }

    [HttpPost("{key}/preview")]
    [RequireAction(Actions.NotificationTemplates.View)]
    [EnableRateLimiting(NotificationRateLimitPolicies.TemplatePreview)]
    public async Task<IActionResult> Preview(string key, [FromBody] PreviewNotificationTemplateRequest request)
    {
        var query = new PreviewNotificationTemplateQuery
        {
            Key = key,
            // Gate fix 2026-09-27 (SECURITY LOW): a `contents:[null]` body used to NPE right here.
            // Null entries are now carried through -- PreviewNotificationTemplateQueryHandler
            // rejects them as 400.
            Contents = request.Contents.Select(c => c is null ? null! : new PreviewTemplateContentItem
            {
                Channel = c.Channel, Subject = c.Subject, Title = c.Title, Body = c.Body, LinkPath = c.LinkPath
            }).ToList(),
            SampleValues = request.SampleValues,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };

        var response = await dispatcher.GetResultAsync(query);
        var result = response.Result!;

        if (result.ErrorCode is not null)
        {
            var validationResponse = new Platform.Infrastructure.Core.Validation.ValidationResponse();
            validationResponse.AddError(errorMessage: result.ErrorMessage!, propertyName: result.ErrorProperty ?? "Contents", errorCode: result.ErrorCode, resourceName: "NotificationTemplate");
            return BadRequest(validationResponse);
        }

        return Ok(result);
    }
}
