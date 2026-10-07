namespace CatEcommerce.Shared.SharedDto.Account;

/// <summary>
/// HO-ACC-001 §12.5 Amended (18) / ADR-342 (owner decision A, 2026-09-25). Returned by
/// <c>RefreshCustomerSessionCsrfTokenCommand</c>/<c>RefreshStaffSessionCsrfTokenCommand</c> when
/// the session middleware re-issues <c>pc_csrf</c> on a safe request whose cookie was missing or
/// stale. The plaintext exists only here and in the cookie the middleware writes from it --
/// only its hash is persisted, on the session row.
/// </summary>
public sealed record CsrfRefreshResult(string Token);
