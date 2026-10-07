using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Administration;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 / ADR-053 -- maintenance mode toggle. Never gated by maintenance
/// mode itself.</summary>
[ApiController]
[Route("api/admin/system")]
public sealed class AdminSystemController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpPut("maintenance")]
    [RequireAction(Actions.SystemStatus.ManageMaintenance)]
    public async Task<IActionResult> SetMaintenance([FromBody] SetMaintenanceModeRequest request)
    {
        var command = new SetMaintenanceModeCommand
        {
            MaintenanceMode = request.MaintenanceMode, Message = request.Message,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : BadRequest(response.ValidationResult);
    }
}
