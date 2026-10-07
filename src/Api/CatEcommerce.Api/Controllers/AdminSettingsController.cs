using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Administration;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 -- business settings.</summary>
[ApiController]
[Route("api/admin/settings")]
public sealed class AdminSettingsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Settings.View)]
    public async Task<IActionResult> Get([FromQuery] Guid? branchId)
    {
        var response = await dispatcher.GetResultAsync(new GetSettingsQuery { BranchId = branchId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPut]
    [RequireAction(Actions.Settings.Manage)]
    public async Task<IActionResult> Change([FromQuery] Guid? branchId, [FromBody] ChangeSettingsRequest request)
    {
        var command = new ChangeBusinessSettingsCommand
        {
            BranchId = branchId, Changes = request.Changes.Select(c => new SettingChangeItem { Key = c.Key, Value = c.Value }).ToList(),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }

    [HttpDelete("{key}")]
    [RequireAction(Actions.Settings.Manage)]
    public async Task<IActionResult> ClearOverride(string key, [FromQuery] Guid branchId)
    {
        var command = new ClearBranchSettingOverrideCommand
        {
            BranchId = branchId, Key = key,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }
}
