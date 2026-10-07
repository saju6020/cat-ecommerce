using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Clinic;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.Commands.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Clinic;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-APT-001 §12.4/§8.11.</summary>
[ApiController]
[Route("api/customers/me/waitlist")]
[RequireCustomerSession]
public sealed class CustomerWaitlistController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IBusinessSettings businessSettings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var response = await dispatcher.GetResultAsync(new GetCustomerWaitlistQuery
        { CustomerId = userContextProvider.GetUserContext().UserId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return Ok(response.Result);
    }

    [HttpPost]
    [EnableRateLimiting(AppointmentRateLimitPolicies.Change)]
    public async Task<IActionResult> Join([FromBody] JoinWaitlistRequest request)
    {
        var waitlistEntryId = Guid.NewGuid();
        var command = new JoinWaitlistCommand
        {
            WaitlistEntryId = waitlistEntryId, BranchId = request.BranchId, ServiceId = request.ServiceId, CatId = request.CatId,
            LocalDate = request.LocalDate, PreferredPerformerId = request.PreferredPerformerId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        if (!response.IsSuccess)
        {
            var codes = response.ValidationResult?.Errors.Select(e => e.ErrorCode).ToHashSet(StringComparer.Ordinal) ?? [];
            if (codes.Contains(BusinessViolationCodes.CatNotFound)) return NotFound();
            return BadRequest(response.ValidationResult);
        }

        var claimWindowMinutes = businessSettings.GetInt(SettingKeys.BookingWaitlistClaimMinutes, request.BranchId);
        return StatusCode(201, new { waitlistEntryId, claimWindowMinutes });
    }

    [HttpDelete("{waitlistEntryId:guid}")]
    public async Task<IActionResult> Withdraw(Guid waitlistEntryId)
    {
        var response = await dispatcher.SendAsync(new WithdrawFromWaitlistCommand
        { WaitlistEntryId = waitlistEntryId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }
}
