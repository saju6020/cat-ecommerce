using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-PHM-001 §12.4 -- what every Pharmacy controller shares: the active branch (HO-ADM-001 §8.3, ADR-046 -- a refused <c>X-Branch-Id</c> is a <c>403</c>
/// naming the refusal, never silently downgraded), the caller's roles, the explicit clinical-record access log (NFR-SEC-8, HO-EMR-001 ADR-129), and the ONE
/// mapping from a returned business violation to an HTTP status (ADR-014): <c>404</c> when it says the record the route addresses is not the caller's to
/// see, <c>409</c> for a stale write or changed warnings, <c>403</c> for a defence-in-depth role refusal, <c>400</c> with the validation shape otherwise.
/// Abstract, so MVC never routes it.
/// </summary>
public abstract class PharmacyControllerBase(IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    private static readonly HashSet<string> ConflictCodes =
    [
        BusinessViolationCodes.StaleVersion, BusinessViolationCodes.PrescriptionWarningsChanged,
    ];

    protected bool TryResolveBranch(out Guid branchId, out IActionResult? refusal)
    {
        var active = activeBranchAccessor.GetActiveBranchId();
        if (active.BranchId is { } id)
        {
            branchId = id;
            refusal = null;
            return true;
        }

        branchId = default;
        refusal = StatusCode(403, new { error = active.Refusal?.ToString() ?? "branch_required" });
        return false;
    }

    protected IReadOnlyList<string> CallerRoles() =>
        HttpContext.Items[ResolvedCaller.HttpContextItemsKey] is ResolvedCaller { Kind: ResolvedCallerKind.Staff } caller ? caller.RoleIds : [];

    /// <summary>
    /// A failed command as an HTTP answer. <paramref name="notFoundCodes"/> are the codes that mean "the record this route addresses is not the caller's to
    /// see" (a missing REFERENCED record -- a medicine in a body -- stays a <c>400</c>). <paramref name="extra"/> (e.g. the safety assessment) is added to the
    /// body for the failures the screen must react to.
    /// </summary>
    protected IActionResult RespondToFailure(CommandResponse response, IReadOnlyDictionary<string, object?>? extra, params string[] notFoundCodes)
    {
        var codes = response.ValidationResult?.Errors.Select(e => e.ErrorCode).ToHashSet(StringComparer.Ordinal) ?? [];

        if (notFoundCodes.Any(codes.Contains)) return NotFound();

        object body = response.ValidationResult!;
        if (extra is { Count: > 0 })
        {
            var merged = new Dictionary<string, object?> { ["errors"] = response.ValidationResult?.Errors, ["isValid"] = false };
            foreach (var pair in extra) merged[pair.Key] = pair.Value;
            body = merged;
        }

        if (codes.Contains(BusinessViolationCodes.PrescribingNotPermitted)) return StatusCode(403, body);

        return codes.Overlaps(ConflictCodes) ? Conflict(body) : BadRequest(body);
    }

    protected IActionResult RespondToFailure(CommandResponse response, params string[] notFoundCodes) => RespondToFailure(response, null, notFoundCodes);

    /// <summary>
    /// NFR-SEC-8 / HO-PHM-001 §4.3 + OQ-12 (adopted by an Amended row now HO-EMR-001's mechanism exists): a clinical read whose cat is only known after it ran
    /// writes its access row here, through EMR's own command. Fails closed -- <c>false</c> means the caller must answer a bare <c>500</c> and release nothing.
    /// </summary>
    protected static async Task<bool> TryLogClinicalAccessAsync(
        IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IClientIpAccessor clientIpAccessor, string path, Guid catId, Guid recordId)
    {
        try
        {
            var response = await dispatcher.SendAsync(new RecordClinicalRecordAccessCommand
            {
                AuditEntryId = Guid.NewGuid(), CatId = catId, Kind = ClinicalRecordKind.ClinicalHistory, RecordId = recordId, Path = path,
                IpAddress = clientIpAccessor.GetClientIpAddress()?.ToString(), UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(),
                ValidateCommand = true
            });
            return response.IsSuccess;
        }
        catch
        {
            return false;
        }
    }
}
