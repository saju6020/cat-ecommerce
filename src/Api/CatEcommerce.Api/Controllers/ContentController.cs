using CatEcommerce.Api.Authorization;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 -- public reads: published only.</summary>
[ApiController]
public sealed class ContentController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet("api/content/{kind}/{slug}")]
    [PublicEndpoint]
    public async Task<IActionResult> GetPage(string kind, string slug)
    {
        var response = await dispatcher.GetResultAsync(new GetPublishedContentQuery { Kind = kind, Slug = slug, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpGet("api/content/banners")]
    [PublicEndpoint]
    public async Task<IActionResult> GetBanners([FromQuery] string? placement)
    {
        if (!EnumQueryParameter.TryParse<BannerPlacement>(placement, out var parsedPlacement))
            return BadRequest(new { error = $"'{placement}' is not a valid banner placement." });

        // Gate fix 2026-09-26: pass the canonical DisplayName, not the caller's raw casing --
        // see AdminContentController.Get's identical fix.
        var response = await dispatcher.GetResultAsync(new GetPublishedBannersQuery { Placement = parsedPlacement?.DisplayName, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpGet("api/content/faqs")]
    [PublicEndpoint]
    public async Task<IActionResult> GetFaqs()
    {
        var response = await dispatcher.GetResultAsync(new GetPublishedFaqsQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }
}
