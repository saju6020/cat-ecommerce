using CatEcommerce.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Api.Sessions;

/// <summary>HO-ACC-001 §12.5 / ADR-031. The only place any of the three session cookies are written.</summary>
public sealed class SessionCookieWriter(IOptions<AccountOptions> accountOptions)
{
    public const string CustomerSessionCookie = "pc_session";
    public const string StaffSessionCookie = "pc_staff_session";
    public const string CsrfCookie = "pc_csrf";
    public const string CsrfHeader = "X-CSRF-Token";

    /// <summary>
    /// SECURITY gate item 7 (first-pass gate fix round, owner decision 2026-09-26) --
    /// <see cref="AccountOptions.CsrfCookieDomain"/>'s own doc comment has the full reasoning.
    /// <c>null</c>/empty (the default, and every environment except a real cross-subdomain
    /// production deployment) means "don't set Domain at all", i.e. host-only, unchanged from
    /// before this option existed.
    /// </summary>
    private string? CsrfCookieDomain =>
        string.IsNullOrEmpty(accountOptions.Value.CsrfCookieDomain) ? null : accountOptions.Value.CsrfCookieDomain;

    public void WriteCustomerSession(HttpContext context, string token, DateTimeOffset expiresOn) =>
        WriteSessionCookie(context, CustomerSessionCookie, token, expiresOn);

    public void WriteStaffSession(HttpContext context, string token, DateTimeOffset expiresOn) =>
        WriteSessionCookie(context, StaffSessionCookie, token, expiresOn);

    /// <summary>
    /// SECURITY gate HIGH (second pass) / ADR-342 (rewritten, owner decision A, 2026-09-25): the
    /// raw value is an independent, per-session random token, generated the same way as the
    /// session token itself (<see cref="CatEcommerce.Domain.DomainServices.ISessionTokenIssuer"/>)
    /// and passed in here by the caller -- it is never derived from a process-lifetime key. Only
    /// its hash is persisted, on the session row itself, so it is durable across a restart exactly
    /// like the session it protects. This method never computes anything; it only writes what it
    /// is given.
    /// </summary>
    public void WriteCsrf(HttpContext context, string csrfToken, DateTimeOffset expiresOn)
    {
        context.Response.Cookies.Append(CsrfCookie, csrfToken, new CookieOptions
        {
            HttpOnly = false, // the client must be able to read it, to echo it in X-CSRF-Token
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Domain = CsrfCookieDomain,
            Expires = expiresOn
        });
    }

    public void ExpireCustomerSession(HttpContext context) => Expire(context, CustomerSessionCookie, domain: null);
    public void ExpireStaffSession(HttpContext context) => Expire(context, StaffSessionCookie, domain: null);

    /// <summary>
    /// SECURITY gate item 7 -- a `Delete`/expiry write for `pc_csrf` MUST carry the same `Domain`
    /// it was originally set with, or the browser treats it as an entirely different cookie (host-
    /// only vs domain-scoped are different cookie jars for the same name/path) and never actually
    /// removes the real one; it would just add a second, expired, host-only `pc_csrf` alongside
    /// the still-live domain-scoped one.
    /// </summary>
    public void ExpireCsrf(HttpContext context) => Expire(context, CsrfCookie, CsrfCookieDomain);

    private static void WriteSessionCookie(HttpContext context, string name, string token, DateTimeOffset expiresOn) =>
        context.Response.Cookies.Append(name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expiresOn
        });

    private static void Expire(HttpContext context, string name, string? domain) =>
        context.Response.Cookies.Append(name, "", new CookieOptions
        {
            HttpOnly = name != CsrfCookie,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Domain = domain,
            Expires = DateTimeOffset.UnixEpoch
        });
}
