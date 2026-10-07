using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Clinic;
using CatEcommerce.Application.Commands.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-APT-001 §12.4/§11.4 C8.</summary>
[ApiController]
[Route("api/staff/walk-ins")]
public sealed class StaffWalkInsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    [HttpPost]
    [RequireAction(Actions.Appointments.Book)]
    public async Task<IActionResult> Register([FromBody] RegisterWalkInRequest request)
    {
        var active = activeBranchAccessor.GetActiveBranchId();
        if (active.BranchId is not { } branchId)
            return StatusCode(403, new { error = active.Refusal?.ToString() ?? "branch_required" });

        var appointmentId = Guid.NewGuid();
        var command = new RegisterWalkInCommand
        {
            AppointmentId = appointmentId, BranchId = branchId, CustomerId = request.CustomerId, CatId = request.CatId,
            ServiceId = request.ServiceId, TriagePriority = request.TriagePriority, PresentingComplaint = request.PresentingComplaint,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { appointmentId }) : BadRequest(response.ValidationResult);
    }
}
