using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Surgery;
using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.Queries.Surgery;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-SRG-001 §12.4 (US-SRG-07, ADR-167) -- per-service surgery protocols. Reads need <c>Surgery.View</c>; writes <c>Surgery.ManageProtocols</c> (Vet, Administrator).</summary>
[ApiController]
[Route("api/staff/surgery-protocols")]
public sealed class StaffSurgeryProtocolsController(
    IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IActiveBranchAccessor activeBranchAccessor)
    : SurgeryControllerBase(activeBranchAccessor)
{
    [HttpGet]
    [RequireAction(Actions.Surgery.View)]
    public async Task<IActionResult> List()
    {
        var response = await dispatcher.GetResultAsync(new GetSurgeryProtocolsQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        return Ok(response.Result);
    }

    [HttpPut("{serviceId:guid}")]
    [RequireAction(Actions.Surgery.ManageProtocols)]
    public async Task<IActionResult> Upsert(Guid serviceId, [FromBody] UpsertSurgeryProtocolRequest request)
    {
        var response = await dispatcher.SendAsync(new UpsertSurgeryProtocolCommand
        {
            ServiceId = serviceId, FastingInstructions = request.FastingInstructions, FastingHours = request.FastingHours, PostOpInstructions = request.PostOpInstructions,
            FollowUps = request.FollowUps.Select(f => new SurgeryProtocolFollowUpInput(f.Kind, f.DayOffset, f.WindowFromDay, f.WindowToDay)).ToList(),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? Ok(new { serviceId }) : RespondToFailure(response);
    }
}
