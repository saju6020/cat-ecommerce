namespace CatEcommerce.Shared.SharedDto.Account;

/// <summary>
/// HO-ACC-001 §11.4 (C2/C12). The session token plaintext exists only on this result and in the
/// cookie the controller writes from it -- never logged, never persisted a second time (§20.2).
/// <see cref="CsrfToken"/> is the same: a second, independent plaintext that exists only here and
/// in the <c>pc_csrf</c> cookie the controller writes from it (ADR-342, owner decision A,
/// 2026-09-25) -- only its hash is persisted, on the session row.
/// </summary>
public sealed record SignInResult(string Token, DateTimeOffset ExpiresOn, Guid AccountId, string CsrfToken);
