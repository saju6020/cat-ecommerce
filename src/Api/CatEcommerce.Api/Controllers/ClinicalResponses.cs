using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-EMR-001 §12.4 -- the failure mapping every clinical controller shares. ADR-014: an expected business
/// outcome is a <c>400</c> carrying the <c>ValidationResponse</c>; HO-ADM-001 §8.2 / backlog #8: a record that
/// does not exist <i>or belongs to another branch</i> is the same <c>404</c>, never a reason that would
/// confirm another branch's id; a stale <c>ExpectedVersion</c> is the same <c>409</c> shape a
/// <c>DbUpdateConcurrencyException</c> produces.
/// </summary>
internal static class ClinicalResponses
{
    /// <summary>The "record named by the route" codes. A missing <i>referenced</i> record (a diagnosis code, a cat)
    /// is the caller's mistake and stays a <c>400</c>.</summary>
    private static readonly HashSet<string> NotFoundCodes =
    [
        BusinessViolationCodes.VisitNotFound, BusinessViolationCodes.AppointmentNotFound,
        BusinessViolationCodes.DiagnosticOrderNotFound, BusinessViolationCodes.ConsentNotFound, BusinessViolationCodes.TreatmentLineNotFound,
    ];

    public static IActionResult Failure(ControllerBase controller, CommandResponse response)
    {
        var codes = response.ValidationResult?.Errors.Select(e => e.ErrorCode).ToHashSet(StringComparer.Ordinal) ?? [];

        if (codes.Overlaps(NotFoundCodes)) return controller.NotFound();

        if (codes.Contains(BusinessViolationCodes.StaleVisitVersion))
            return controller.Conflict(new { error = ClinicalMessages.StaleVisit });

        // HO-EMR-001 section 8.6 steps 5 and 8. InsufficientStock WITH detail is the warning ("only 3 in the dispensary"): a 400 the client answers
        // with acceptShortfall. WITHOUT detail it is the race: the last units went between the read and the deduction (IMP-5) -- nothing was
        // committed, the client re-reads the batches, and the answer is a 409 like the commit-time CHECK's.
        if (codes.Contains(BusinessViolationCodes.InsufficientStock))
        {
            return response.Result is InsufficientStockDetail detail
                ? FailureWith(controller, response.ValidationResult, new Dictionary<string, object?>
                {
                    ["availableQuantity"] = InventoryViewFormat.Quantity(detail.AvailableQuantity), ["unit"] = detail.Unit
                })
                : controller.Conflict(new { error = InventoryMessages.InsufficientStock });
        }

        return controller.BadRequest(response.ValidationResult);
    }

    /// <summary>A <c>400</c> whose body is the usual validation body plus one extra member (e.g. <c>outOfRange</c>).</summary>
    public static IActionResult FailureWith(ControllerBase controller, ValidationResponse? validation, string extraName, object? extra)
    {
        var body = new Dictionary<string, object?>
        {
            ["errors"] = validation?.Errors,
            ["isValid"] = false,
            [extraName] = extra,
        };

        return controller.BadRequest(body);
    }

    /// <summary>A <c>400</c> whose body is the usual validation body plus several extra members (e.g. <c>availableQuantity</c> and <c>unit</c>).</summary>
    public static IActionResult FailureWith(ControllerBase controller, ValidationResponse? validation, IReadOnlyDictionary<string, object?> extras)
    {
        var body = new Dictionary<string, object?> { ["errors"] = validation?.Errors, ["isValid"] = false };
        foreach (var (name, value) in extras) body[name] = value;

        return controller.BadRequest(body);
    }

    /// <summary>The caller's active branch, or the <c>403</c> naming why there is none (ADR-046).</summary>
    public static bool TryResolveBranch(ControllerBase controller, IActiveBranchAccessor accessor, out Guid branchId, out IActionResult? refusal)
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

    /// <summary>The discharge summary as a PDF: rendered per request (never stored, ADR-130) and never cached; the file name
    /// carries nothing about the cat or the visit.</summary>
    public static IActionResult Pdf(ControllerBase controller, IDischargeSummaryPdfRenderer renderer, CatEcommerce.Read.ViewModels.DischargeSummaryViewModel summary)
    {
        var bytes = renderer.Render(CatEcommerce.Read.QueryHandlers.Visit.DischargeSummaryDocuments.From(summary));

        controller.Response.Headers.CacheControl = "private, no-store";
        controller.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return controller.File(bytes, "application/pdf", "visit-summary.pdf");
    }

    /// <summary>The staff roles of the resolved caller (empty for anyone else).</summary>
    public static IReadOnlyList<string> CallerRoles(ControllerBase controller) =>
        controller.HttpContext.Items[ResolvedCaller.HttpContextItemsKey] is ResolvedCaller { Kind: ResolvedCallerKind.Staff } caller ? caller.RoleIds : [];
}
