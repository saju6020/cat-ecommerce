using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Api.Ordering;

/// <summary>
/// HO-ORD-001 §12.4 -- how a failed Ordering command becomes an HTTP answer. A violation that says "this record does not exist (for you)" is a
/// <c>404</c> -- the same answer as a record that belongs to someone else, so a customer can never tell the two apart (§12.3, I-E11); a
/// concurrency or stock race the client must re-read is a <c>409</c>; every other business refusal is a <c>400</c> with the usual body.
/// </summary>
public static class OrderingResults
{
    private static readonly HashSet<string> NotFoundCodes =
    [
        BusinessViolationCodes.DeliveryZoneNotFound, BusinessViolationCodes.CourierNotFound, BusinessViolationCodes.DiscountCodeNotFound,
        BusinessViolationCodes.OrderNotFound, BusinessViolationCodes.ReturnNotFound, BusinessViolationCodes.CartLineNotFound,
        BusinessViolationCodes.ReconciliationIssueNotFound, BusinessViolationCodes.CatNotOwnedByCaller,
    ];

    private static readonly HashSet<string> ConflictCodes =
    [
        BusinessViolationCodes.CheckoutQuoteChanged, BusinessViolationCodes.StockNoLongerAvailable, BusinessViolationCodes.DuplicateSubmission,
        BusinessViolationCodes.RequestInProgress, BusinessViolationCodes.StaleVersion,
    ];

    public static IActionResult Failure(ControllerBase controller, CommandResponse response)
    {
        var codes = response.ValidationResult?.Errors.Select(e => e.ErrorCode).ToList() ?? [];

        if (codes.Any(NotFoundCodes.Contains)) return controller.NotFound();
        if (codes.Any(ConflictCodes.Contains)) return controller.Conflict(response.ValidationResult);
        return controller.BadRequest(response.ValidationResult);
    }
}
