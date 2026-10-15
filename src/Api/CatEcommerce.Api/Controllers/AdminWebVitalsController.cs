using CatEcommerce.Api.Authorization;
using CatEcommerce.Read.Queries.Operations;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-XCT-001 section 12.4 Q2 (US-XCT-02). The p75 of each budgeted web vital per route and viewport over a date range of at most 93 days.</summary>
[ApiController]
public sealed class AdminWebVitalsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet("api/admin/web-vitals/summary")]
    [RequireAction(Actions.Telemetry.View)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? routeTemplate, [FromQuery] string? viewportClass)
    {
        if (from is null || to is null || to < from || to.Value.DayNumber - from.Value.DayNumber + 1 > WebVitalBudgets.MaxRangeDays)
        {
            var invalid = new ValidationResponse();
            invalid.AddError(
                errorMessage: $"Give a from and to date, at most {WebVitalBudgets.MaxRangeDays} days apart.", propertyName: "from",
                errorCode: BusinessViolationCodes.InvalidDateRange, resourceName: nameof(AdminWebVitalsController));
            return BadRequest(invalid);
        }

        if (!string.IsNullOrWhiteSpace(viewportClass) && !CatEcommerce.Shared.Common.Enums.Enumeration.GetAll<CatEcommerce.Shared.Common.Enums.ViewportClass>().Any(v => v.DisplayName == viewportClass))
            return BadRequest(new { error = $"'{viewportClass}' is not a valid viewport class." });

        var response = await dispatcher.GetResultAsync(new GetWebVitalsSummaryQuery
        {
            From = new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            To = new DateTimeOffset(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            RouteTemplate = routeTemplate, ViewportClass = viewportClass,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        });

        return Ok(response.Result);
    }
}
