using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Administration;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 -- pages, FAQs, banners and policy documents.</summary>
[ApiController]
[Route("api/admin/content")]
public sealed class AdminContentController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Content.Manage)]
    public async Task<IActionResult> Get([FromQuery] string? kind)
    {
        if (!EnumQueryParameter.TryParse<ContentKind>(kind, out var parsedKind))
            return BadRequest(new { error = $"'{kind}' is not a valid content kind." });

        // Gate fix 2026-09-26: pass the canonical DisplayName on, not the caller's raw casing --
        // the handler's Enumeration.FromDisplayName is case-sensitive, so e.g. "page" (valid per
        // TryParse's OrdinalIgnoreCase match) previously still 500'd past this 400 guard.
        var response = await dispatcher.GetResultAsync(new GetAdminContentQuery { Kind = parsedKind?.DisplayName, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpGet("{id:guid}")]
    [RequireAction(Actions.Content.Manage)]
    public async Task<IActionResult> GetOne(Guid id)
    {
        var response = await dispatcher.GetResultAsync(new GetAdminContentItemQuery { ContentItemId = id, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Content.Manage)]
    public async Task<IActionResult> Create([FromBody] CreateContentItemRequest request)
    {
        var contentItemId = Guid.NewGuid();
        var command = new CreateContentItemCommand
        {
            ContentItemId = contentItemId, Kind = request.Kind, Slug = request.Slug, Title = request.Title, DraftBody = request.DraftBody,
            Placement = request.Placement, LinkUrl = request.LinkUrl, VisibleFrom = request.VisibleFrom, VisibleTo = request.VisibleTo, SortOrder = request.SortOrder,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { contentItemId }) : BadRequest(response.ValidationResult);
    }

    [HttpPut("{id:guid}")]
    [RequireAction(Actions.Content.Manage)]
    public async Task<IActionResult> EditDraft(Guid id, [FromBody] EditContentDraftRequest request)
    {
        var command = new EditContentDraftCommand
        {
            ContentItemId = id, Title = request.Title, DraftBody = request.DraftBody, Placement = request.Placement,
            LinkUrl = request.LinkUrl, VisibleFrom = request.VisibleFrom, VisibleTo = request.VisibleTo, SortOrder = request.SortOrder,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpPost("{id:guid}/publish")]
    [RequireAction(Actions.Content.Manage)]
    public async Task<IActionResult> Publish(Guid id)
    {
        var response = await dispatcher.SendAsync(new PublishContentItemCommand { ContentItemId = id, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpPost("{id:guid}/unpublish")]
    [RequireAction(Actions.Content.Manage)]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        var response = await dispatcher.SendAsync(new UnpublishContentItemCommand { ContentItemId = id, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }
}
