using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Administration;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-ADM-001 §12.4 -- policy documents and versions. Owner decision 2026-09-26: create
/// and publish are Administrator-only (<see cref="Actions.Policies"/>.<see cref="Actions.Policies
/// .Manage"/>) for every key; the list view stays on <see cref="Actions.Content"/>.<see
/// cref="Actions.Content.Manage"/>, which a Shop Manager also holds, since a Shop Manager still
/// needs to see the policy list to manage ordinary content alongside it.</summary>
[ApiController]
[Route("api/admin/policies")]
public sealed class AdminPoliciesController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Content.Manage)]
    public async Task<IActionResult> Get()
    {
        var response = await dispatcher.GetResultAsync(new GetAdminPoliciesQuery { UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });
        // Only the outer document list is signalled -- a per-document Versions list independently
        // hitting the same cap cannot be attributed to one document via a single response header
        // without changing the response shape (documented limitation, HO-ADM-001 §1).
        TruncationHeader.ApplyIfAtCap(Response, response.Result.Count);
        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Policies.Manage)]
    public async Task<IActionResult> Create([FromBody] CreatePolicyDocumentRequest request)
    {
        var policyDocumentId = Guid.NewGuid();
        var command = new CreatePolicyDocumentCommand
        {
            PolicyDocumentId = policyDocumentId, Key = request.Key, Title = request.Title, RequiresAcceptance = request.RequiresAcceptance,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { policyDocumentId }) : BadRequest(response.ValidationResult);
    }

    [HttpPost("{key}/versions")]
    [RequireAction(Actions.Policies.Manage)]
    public async Task<IActionResult> PublishVersion(string key, [FromBody] PublishPolicyVersionRequest request)
    {
        var policyVersionId = Guid.NewGuid();
        var command = new PublishPolicyVersionCommand
        {
            PolicyVersionId = policyVersionId, PolicyKey = key, Body = request.Body, ChangeSummary = request.ChangeSummary,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { policyVersionId }) : BadRequest(response.ValidationResult);
    }
}
