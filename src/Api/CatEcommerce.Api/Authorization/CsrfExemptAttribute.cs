namespace CatEcommerce.Api.Authorization;

/// <summary>
/// HO-ACC-001 §12.5 Amended (18) / ADR-342 (owner decision A, 2026-09-25). Marks an endpoint the
/// session middleware must never refuse for a missing or mismatched <c>X-CSRF-Token</c> header,
/// regardless of whether a stale or otherwise-resolvable session cookie is still present. Applied
/// only to the two sign-in actions: a customer or staff member re-authenticating must never be
/// locked out by a CSRF check tied to a session they are in the middle of replacing -- a stale
/// session cookie at that endpoint is treated as anonymous.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CsrfExemptAttribute : Attribute;
