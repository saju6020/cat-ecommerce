using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Account;
using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ACC-001 §12.4 / US-ACC-07.</summary>
[ApiController]
[Route("api/staff-accounts")]
public sealed class StaffAccountsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.StaffAccounts.View)]
    public async Task<IActionResult> GetStaffAccounts([FromQuery] string? search, [FromQuery] string? roleId,
        [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        // Integrator fix 2026-09-26 (HO-ADM-001-SECURITY's carried line: "HO-ACC-001:
        // GET /api/staff-accounts?pageNumber=2147483647 -> 500 (the Skip overflow)"):
        // (pageNumber-1)*pageSize can overflow int (Skip overflow) for a very large caller-supplied
        // pageNumber/pageSize -- the same class of gap AdminLeaveRequestsController already closed
        // with the shared PagingRules cap, applied here the same way.
        if (pageNumber is > PagingRules.MaxPageNumber) return BadRequest(new { error = $"pageNumber cannot exceed {PagingRules.MaxPageNumber}." });
        if (pageSize is > PagingRules.MaxPageSize) return BadRequest(new { error = $"pageSize cannot exceed {PagingRules.MaxPageSize}." });

        var query = new GetStaffAccountsQuery
        {
            Search = search,
            RoleId = roleId,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        };
        ApplyPaging(query, pageNumber, pageSize);

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("{staffAccountId:guid}")]
    [RequireAction(Actions.StaffAccounts.View)]
    public async Task<IActionResult> GetStaffAccount(Guid staffAccountId)
    {
        var response = await dispatcher.GetResultAsync(new GetStaffAccountQuery
        {
            StaffAccountId = staffAccountId,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid()
        });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.StaffAccounts.Create)]
    public async Task<IActionResult> CreateStaffAccount([FromBody] CreateStaffAccountRequest request)
    {
        var staffAccountId = Guid.NewGuid();
        var command = new CreateStaffAccountCommand
        {
            StaffAccountId = staffAccountId,
            FullName = request.FullName,
            MobileNumber = request.MobileNumber,
            EmailAddress = request.EmailAddress,
            RoleIds = request.RoleIds,
            InitialPassword = request.Password,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess ? StatusCode(201, new { staffAccountId }) : BadRequest(response.ValidationResult);
    }

    /// <summary>Owner, 2026-10-20: an administrator edits a staff member's name, mobile number and email address.</summary>
    [HttpPut("{staffAccountId:guid}")]
    [RequireAction(Actions.StaffAccounts.UpdateDetails)]
    public async Task<IActionResult> UpdateDetails(Guid staffAccountId, [FromBody] UpdateStaffAccountDetailsRequest request)
    {
        var command = new UpdateStaffAccountDetailsCommand
        {
            StaffAccountId = staffAccountId,
            FullName = request.FullName,
            MobileNumber = request.MobileNumber,
            EmailAddress = request.EmailAddress,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        if (response.IsSuccess) return NoContent();
        var notFound = response.ValidationResult?.Errors?.Any(e => e.ErrorCode == BusinessViolationCodes.StaffAccountNotFound) == true;
        return notFound ? NotFound() : BadRequest(response.ValidationResult);
    }

    /// <summary>Owner, 2026-10-19: an administrator sets a staff member's password and shares it with them.</summary>
    [HttpPut("{staffAccountId:guid}/password")]
    [RequireAction(Actions.StaffAccounts.SetPassword)]
    public async Task<IActionResult> SetPassword(Guid staffAccountId, [FromBody] SetStaffPasswordRequest request)
    {
        var command = new SetStaffPasswordCommand
        {
            StaffAccountId = staffAccountId,
            NewPassword = request.Password,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        if (response.IsSuccess) return NoContent();
        var notFound = response.ValidationResult?.Errors?.Any(e => e.ErrorCode == BusinessViolationCodes.StaffAccountNotFound) == true;
        return notFound ? NotFound() : BadRequest(response.ValidationResult);
    }

    [HttpPut("{staffAccountId:guid}/roles")]
    [RequireAction(Actions.StaffAccounts.ChangeRoles)]
    public async Task<IActionResult> ChangeRoles(Guid staffAccountId, [FromBody] ChangeStaffRolesRequest request)
    {
        var command = new ChangeStaffRolesCommand
        {
            StaffAccountId = staffAccountId,
            RoleIds = request.RoleIds,
            UserContext = userContextProvider.GetUserContext(),
            CorrelationId = Guid.NewGuid(),
            ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);

        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    private static void ApplyPaging<T>(Platform.Infrastructure.Core.Queries.Query<T> query, int? pageNumber, int? pageSize)
    {
        if (pageNumber is not null) query.PageNumber = pageNumber.Value;
        if (pageSize is not null) query.PageSize = pageSize.Value;
    }
}
