using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Contracts.Inventory;

/// <summary>
/// HO-IMP-001 §12.3/§12.4 -- the two things every Inventory controller does the same way, in one place:
/// resolve the caller's ACTIVE branch (<c>X-Branch-Id</c>, HO-ADM-001 §8.3 -- a refusal is a 403 naming the refusal code, exactly
/// like <c>StaffAppointmentsController</c>), and turn a failed <see cref="CommandResponse"/> into the right status. "Not found" outcomes
/// (the record the request ADDRESSES is missing or another branch's -- the handler marks it with <c>InventoryRules.NotFoundResource</c>) are 404; a record
/// merely referenced in a body is a 400. A record of another branch never leaks its existence, a stale version is 409, everything else is the 400 body of
/// <c>ValidationResponse</c> (ADR-014). Never 202.
/// </summary>
public static class InventoryControllerSupport
{
    private static readonly HashSet<string> ConflictCodes = new(StringComparer.Ordinal)
    {
        BusinessViolationCodes.ConsignmentVersionConflict,
    };

    public static bool TryResolveBranch(this ControllerBase controller, IActiveBranchAccessor accessor, out Guid branchId, out IActionResult? refusal)
    {
        var active = accessor.GetActiveBranchId();
        if (active.BranchId is { } id)
        {
            branchId = id;
            refusal = null;
            return true;
        }

        branchId = default;
        refusal = controller.StatusCode(403, new { error = active.Refusal?.ToString() ?? "branch_required" });
        return false;
    }

    /// <summary>The failure half of a command's response: 404, 409 or 400.</summary>
    public static IActionResult Failure(this ControllerBase controller, CommandResponse response)
    {
        var errors = response.ValidationResult?.Errors.ToList() ?? [];
        var codes = errors.Select(e => e.ErrorCode).ToHashSet(StringComparer.Ordinal);
        if (errors.Any(e => e.ResourceName == InventoryRules.NotFoundResource)) return controller.NotFound();
        if (codes.Any(ConflictCodes.Contains)) return controller.Conflict(response.ValidationResult);
        return controller.BadRequest(response.ValidationResult);
    }

    /// <summary>204 on success, otherwise the failure.</summary>
    public static IActionResult NoContentOrFailure(this ControllerBase controller, CommandResponse response) =>
        response.IsSuccess ? controller.NoContent() : controller.Failure(response);

    /// <summary>201 with <paramref name="body"/> on success, otherwise the failure.</summary>
    public static IActionResult CreatedOrFailure(this ControllerBase controller, CommandResponse response, object body) =>
        response.IsSuccess ? controller.StatusCode(201, body) : controller.Failure(response);

    /// <summary>A 400 for a malformed query parameter, in the same body shape as every other validation failure.</summary>
    public static IActionResult InvalidQuery(this ControllerBase controller, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property, errorCode: "InvalidQuery", resourceName: "Inventory");
        return controller.BadRequest(validationResponse);
    }
}
