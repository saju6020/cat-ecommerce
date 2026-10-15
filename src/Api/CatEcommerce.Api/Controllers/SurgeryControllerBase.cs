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
/// HO-SRG-001 §12.4 -- what every Surgery controller shares: the active branch (HO-ADM-001 §8.3, ADR-046 -- a refused <c>X-Branch-Id</c> is a <c>403</c> naming the
/// refusal), the caller's roles, the explicit clinical-record access log (NFR-SEC-8, HO-EMR-001 ADR-129 -- the cat of a surgery is only known after it is loaded, so
/// the row is written here rather than by the route-driven filter), and the ONE mapping from a returned business violation to an HTTP status (ADR-014):
/// <c>404</c> when the record the route addresses is not the caller's to see, <c>409</c> for a stale write or an immutable record, <c>403</c> for a defence-in-depth
/// role refusal, <c>400</c> with the validation shape otherwise. Abstract, so MVC never routes it.
/// </summary>
public abstract class SurgeryControllerBase(IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    private static readonly HashSet<string> ConflictCodes =
    [
        BusinessViolationCodes.StaleVersion, BusinessViolationCodes.ReportNotEditable, BusinessViolationCodes.ReportAlreadyFinalised,
        BusinessViolationCodes.RecordIsImmutable, BusinessViolationCodes.AnaesthesiaEntryAlreadyCorrected, BusinessViolationCodes.DuplicateAnaesthesiaEntry,
        BusinessViolationCodes.AnaesthesiaEntryRaced, BusinessViolationCodes.AnaesthesiaEntryIdMismatch,
    ];

    private static readonly HashSet<string> ForbiddenCodes = [BusinessViolationCodes.ConsentOverrideNotPermitted, BusinessViolationCodes.VeterinarianRequired];

    /// <summary>The "record named by the route" codes. A missing REFERENCED record (a medicine, a batch) is the caller's mistake and stays a <c>400</c>.</summary>
    protected static readonly string[] RouteNotFoundCodes = [BusinessViolationCodes.SurgeryNotFound, BusinessViolationCodes.SurgicalReportNotFound, BusinessViolationCodes.SurgeryCampaignNotFound];

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

    /// <summary>A failed command as an HTTP answer. <paramref name="notFoundCodes"/> are the codes that mean "the record this route addresses is not the caller's to see".</summary>
    protected IActionResult RespondToFailure(CommandResponse response, params string[] notFoundCodes)
    {
        var codes = response.ValidationResult?.Errors.Select(e => e.ErrorCode).ToHashSet(StringComparer.Ordinal) ?? [];

        if (notFoundCodes.Any(codes.Contains)) return NotFound();

        object body = (object?)response.ValidationResult ?? new { error = "The request could not be completed." };
        if (codes.Overlaps(ForbiddenCodes)) return StatusCode(403, body);
        return codes.Overlaps(ConflictCodes) ? Conflict(body) : BadRequest(body);
    }

    /// <summary>NFR-SEC-8 -- a clinical read writes its access row through EMR's own command. Fails closed: <c>false</c> means the caller must answer a bare
    /// <c>500</c> and release nothing.</summary>
    protected static async Task<bool> TryLogClinicalAccessAsync(
        IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider, IClientIpAccessor clientIpAccessor, string path, Guid? catId, Guid recordId)
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

    protected IActionResult ClinicalAccessFailed() =>
        new ObjectResult(new { error = "The request could not be completed." }) { StatusCode = StatusCodes.Status500InternalServerError };
}
