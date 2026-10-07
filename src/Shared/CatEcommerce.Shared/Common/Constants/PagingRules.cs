namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Gate fix 2026-09-26 (SEC LOW). <c>Query&lt;T&gt;.Skip</c> is computed as
/// <c>(PageNumber-1)*PageSize</c>, which overflows <c>int</c> for a very large caller-supplied
/// <c>pageNumber</c> (e.g. <c>int.MaxValue</c>), producing a negative <c>OFFSET</c> Postgres
/// rejects -- previously an uncaught 500 on the audit search and, separately, the admin leave-request
/// list. One shared cap, applied at the controller for every paged admin endpoint, instead of a
/// local constant duplicated per controller.
///
/// Gate fix 2026-09-26 (CR LOW, hardening round): <see cref="MaxPageSize"/> closes the other half
/// of the same overflow -- <c>AdminLeaveRequestsController</c> capped <c>pageNumber</c> but not
/// <c>pageSize</c>, so <c>pageSize=1_500_000_000</c> with <c>pageNumber=3</c> still overflowed
/// <c>Skip</c> negative. The audit search already has its own, higher, established cap
/// (<c>AuditExportRules.MaxSearchPageSize</c>, 200, applied by clamping rather than rejecting) and
/// keeps it unchanged; <see cref="MaxPageSize"/> (100) is the general-purpose cap for every other
/// paged admin endpoint, applied by rejecting with 400 -- consistent with how <see
/// cref="MaxPageNumber"/> itself is enforced.
///
/// ARCHITECTURE/CODE_REVIEW gates, second pass (2026-09-26): <see cref="MaxPageSize"/> has two
/// callers with two different enforcement policies, both legitimate -- do not assume the wrong one
/// for a new caller. Every paged **admin** endpoint (<c>AdminAuditEntriesController</c>,
/// <c>AdminLeaveRequestsController</c>) **rejects** a <c>pageSize</c> above the cap with 400. The
/// Cat and Vaccine list endpoints (<c>CatsController</c>/<c>VaccinesController</c>, via the shared
/// <c>Api.Controllers.PagingHelper</c>) **clamp** it to the cap instead (owner decision, HO-CAT-001
/// §1 Amended, 2026-09-26) and return the total via <c>PagedResult&lt;T&gt;</c>, so a clamped
/// request still tells the caller how many rows exist. <see cref="MaxPageNumber"/> is enforced the
/// same way (rejected with 400) by both sets of callers.
/// </summary>
public static class PagingRules
{
    public const int MaxPageNumber = 1_000_000;
    public const int MaxPageSize = 100;
}
