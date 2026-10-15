using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Operations;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-XCT-001 section 12.4 (US-XCT-06, FR-ACC-11, D-11). An administrator takes a customer deletion request, or re-applies erasures after a restore
/// (ADR-314). Both answer with the real result (<c>200</c>), never <c>202</c>.
/// </summary>
[ApiController]
public sealed class AdminCustomerErasureController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpPost("api/admin/customers/{customerAccountId:guid}/erasure")]
    [RequireAction(Actions.CustomerErasure.Erase)]
    public async Task<IActionResult> Erase(Guid customerAccountId, [FromBody] EraseCustomerRequest request)
    {
        var response = await dispatcher.SendAsync(new EraseCustomerOnRequestCommand
        {
            CustomerAccountId = customerAccountId,
            Channel = request.Channel ?? string.Empty,
            IdentityCheck = request.IdentityCheck ?? string.Empty,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        if (response.IsSuccess) return Ok(response.Result);

        var notFound = response.ValidationResult?.Errors.Any(e => e.ErrorCode == BusinessViolationCodes.CustomerAccountNotFound) == true;
        return notFound ? NotFound(response.ValidationResult) : BadRequest(response.ValidationResult);
    }

    [HttpPost("api/admin/retention/erasures/reapply")]
    [RequireAction(Actions.CustomerErasure.Erase)]
    public async Task<IActionResult> Reapply([FromBody] ReapplyErasuresRequest request)
    {
        var response = await dispatcher.SendAsync(new ReapplyErasuresAfterRestoreCommand
        {
            CustomerAccountIds = request.CustomerAccountIds ?? [],
            RestoreReference = request.RestoreReference ?? string.Empty,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return response.IsSuccess ? Ok(response.Result) : BadRequest(response.ValidationResult);
    }
}
