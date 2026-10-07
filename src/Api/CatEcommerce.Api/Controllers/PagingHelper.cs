using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Core.Queries;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// Gate fix 2026-09-26 (second pass, ARCHITECTURE/CODE_REVIEW LOW). Shared pageNumber/pageSize
/// validation and clamping for every Cat/Vaccine list endpoint -- previously duplicated between
/// <c>CatsController.ApplyPaging</c> and <c>VaccinesController.GetVaccines</c>, with
/// <c>VaccinesController</c> calling <c>CatsController</c>'s static helper directly (a
/// controller-to-controller dependency). Both controllers now call this instead.
///
/// <c>pageSize</c> is clamped to <see cref="PagingRules.MaxPageSize"/>, not rejected (owner
/// decision, HO-CAT-001 §1 Amended, 2026-09-26) -- <see cref="PagingRules"/>'s own comment now
/// documents both of its callers' policies. <c>pageNumber</c> above
/// <see cref="PagingRules.MaxPageNumber"/> is rejected with 400
/// (<see cref="BusinessViolationCodes.InvalidPageNumber"/>), the same policy
/// <c>AdminAuditEntriesController</c> and <c>AdminLeaveRequestsController</c> already use for the
/// identical <c>Query&lt;T&gt;.Skip</c> int-overflow risk -- <c>(PageNumber-1)*PageSize</c>
/// overflowing <c>int</c> to a negative <c>OFFSET</c> Postgres rejects with an unhandled 500
/// (SECURITY/CODE_REVIEW gates, second pass, proven live at <c>pageNumber=2147483647</c>).
/// </summary>
internal static class PagingHelper
{
    /// <summary>
    /// Returns the 400 <see cref="IActionResult"/> on an invalid <paramref name="pageNumber"/> or
    /// <paramref name="pageSize"/>, or <c>null</c> when paging was applied (or omitted)
    /// successfully. The 400 body is the same <c>ValidationResponse</c> shape every other
    /// validation failure returns (ADR-014).
    /// </summary>
    public static IActionResult? ApplyPaging<TResult>(Query<TResult> query, int? pageNumber, int? pageSize)
    {
        if (pageNumber is > PagingRules.MaxPageNumber)
            return new BadRequestObjectResult(InvalidPageNumberValidationResponse());

        if (pageNumber is > 0) query.PageNumber = pageNumber.Value;

        if (pageSize is not null)
        {
            if (pageSize <= 0)
                return new BadRequestObjectResult(InvalidPageSizeValidationResponse());

            query.PageSize = Math.Min(pageSize.Value, PagingRules.MaxPageSize);
        }

        return null;
    }

    internal static ValidationResponse InvalidPageSizeValidationResponse()
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(
            errorMessage: "pageSize must be greater than zero.",
            propertyName: "pageSize",
            errorCode: BusinessViolationCodes.InvalidPageSize,
            resourceName: nameof(PagingHelper));
        return validationResponse;
    }

    internal static ValidationResponse InvalidPageNumberValidationResponse()
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(
            errorMessage: $"pageNumber must not exceed {PagingRules.MaxPageNumber}.",
            propertyName: "pageNumber",
            errorCode: BusinessViolationCodes.InvalidPageNumber,
            resourceName: nameof(PagingHelper));
        return validationResponse;
    }
}
