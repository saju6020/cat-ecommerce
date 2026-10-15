using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// HO-BIL-001 §12.4 -- what every staff Billing controller shares: the active branch (HO-ADM-001 §8.3, ADR-046 --
/// a refused <c>X-Branch-Id</c> is a <c>403</c> naming the refusal, never silently downgraded), the
/// <c>Idempotency-Key</c> header of the money-moving writes (ADR-187), and the one mapping from a returned business
/// violation to an HTTP status. Abstract, so MVC never routes it.
/// </summary>
public abstract class StaffBillingControllerBase(IActiveBranchAccessor activeBranchAccessor) : ControllerBase
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

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

    /// <summary>The caller's <c>Idempotency-Key</c>, or null. A key longer than the cap is refused at the edge.</summary>
    protected bool TryReadIdempotencyKey(out string? key, out IActionResult? refusal)
    {
        key = null;
        refusal = null;

        if (!Request.Headers.TryGetValue(IdempotencyKeyHeader, out var values)) return true;

        var value = values.ToString().Trim();
        if (value.Length == 0) return true;

        if (value.Length > BillingRules.MaxIdempotencyKeyLength || value.Any(char.IsControl))
        {
            refusal = BadRequest(new { error = $"{IdempotencyKeyHeader} must be at most {BillingRules.MaxIdempotencyKeyLength} printable characters." });
            return false;
        }

        key = value;
        return true;
    }

    /// <summary>A returned violation becomes: <c>404</c> when it says the addressed record is not the caller's to
    /// see, <c>409</c> for a stale write or an idempotency-key clash, <c>400</c> with the validation shape otherwise.</summary>
    protected IActionResult RespondToFailure(ValidationResponse? validationResult, params string[] notFoundCodes)
    {
        var codes = validationResult?.Errors.Select(e => e.ErrorCode).ToHashSet(StringComparer.Ordinal) ?? [];

        if (notFoundCodes.Any(codes.Contains)) return NotFound();
        if (codes.Contains(BusinessViolationCodes.IdempotencyKeyConflict) || codes.Contains(BusinessViolationCodes.StaleVersion)
            || codes.Contains(BusinessViolationCodes.ChargeAlreadyInvoiced) || codes.Contains(BusinessViolationCodes.ChargeAlreadyRecorded)
            || codes.Contains(BusinessViolationCodes.BankLineAlreadyMatched) || codes.Contains(BusinessViolationCodes.PaymentAlreadyMatched))
            return Conflict(validationResult);

        return BadRequest(validationResult);
    }
}
