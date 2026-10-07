using System.ComponentModel.DataAnnotations;

namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>Bound from appsettings.json section "Account" -- HO-ACC-001 §12.2.</summary>
public sealed class AccountOptions
{
    public const string SectionName = "Account";

    /// <summary>Argon2id memory cost, KiB (ADR-027).</summary>
    public int Argon2MemoryKiB { get; set; } = 19456;

    /// <summary>Argon2id iteration count (ADR-027).</summary>
    public int Argon2Iterations { get; set; } = 2;

    /// <summary>Argon2id parallelism (ADR-027).</summary>
    public int Argon2Parallelism { get; set; } = 1;

    public int CustomerSessionLifetimeHours { get; set; } = 168;

    public int StaffSessionLifetimeHours { get; set; } = 12;

    [Range(5, 240)]
    public int StaffIdleTimeoutMinutes { get; set; } = 30;

    public int PasswordResetTokenLifetimeMinutes { get; set; } = 15;

    /// <summary>
    /// SECURITY gate item 7 (HO-ACC-002 first-pass gate fix round, owner decision 2026-09-26).
    /// The <c>Domain</c> attribute for the <c>pc_csrf</c> cookie only -- <c>null</c>/empty (the
    /// default) means host-only, which is correct for local development, where the web app and
    /// API share the same hostname (<c>localhost</c>, different ports only -- cookie scoping
    /// ignores port). In production the web app and API are deployed on two different subdomains
    /// of one registrable domain (e.g. <c>www.example.com</c> / <c>api.example.com</c>) -- a
    /// host-only <c>pc_csrf</c> set by a response from <c>api.example.com</c> is then NEVER
    /// visible to <c>document.cookie</c> on <c>www.example.com</c>'s own page, which is exactly
    /// what the frontend needs to read it and echo it into <c>X-CSRF-Token</c> (the whole point
    /// of a non-<c>HttpOnly</c> CSRF cookie). Set this to the shared parent domain (e.g.
    /// <c>.example.com</c>, with the leading dot) in that deployment's own appsettings so the
    /// cookie becomes readable across both subdomains. <c>pc_session</c>/<c>pc_staff_session</c>
    /// stay host-only always -- they are <c>HttpOnly</c> and only need the browser to attach them
    /// automatically on requests back to the API's own host; broadening their scope would only
    /// increase exposure for no benefit.
    ///
    /// SECURITY gate optional hardening (second pass): validated at startup (<c>ValidateOnStart</c>,
    /// same as every other option here) rather than left to fail silently as a production 403 --
    /// a malformed value here breaks CSRF entirely (the browser drops the whole cookie if the
    /// Domain does not domain-match the API's own host, RFC 6265 SS5.3, fail-closed, but only
    /// discovered by an operator once real traffic starts failing). Empty is allowed (host-only,
    /// the default). Otherwise it must be a bare hostname, an optional leading dot (the
    /// conventional "this domain and every subdomain" form) followed by one or more
    /// dot-separated, non-empty, alphanumeric-and-hyphen labels -- a scheme (<c>https://</c>),
    /// a port (<c>:5005</c>), a path (<c>/x</c>) or any whitespace all fail the pattern, catching
    /// the operator-typo shapes an accidental copy-paste of a full origin would produce.
    ///
    /// CODE_REVIEW gate third pass, LOW -- an IP literal is also rejected, not only a scheme/port/
    /// path/whitespace typo. RFC 6265 SS5.1.3/SS5.3 requires a browser to compare an IP-address
    /// request host against a cookie's <c>Domain</c> attribute for EXACT string equality, never
    /// subdomain-matching -- setting <c>Domain</c> to an IP address is nonsensical for this option's
    /// entire purpose (making <c>pc_csrf</c> readable across subdomains of a real registrable
    /// domain; an IP address has none), and some browsers refuse the cookie outright rather than
    /// silently degrading to host-only. An IPv6 literal (<c>::1</c>, <c>fe80::1</c>, ...) already
    /// fails the pattern above on its own -- a colon is not in the allowed character set, and no
    /// valid textual IPv6 form omits colons. An IPv4 dotted-quad (<c>127.0.0.1</c>,
    /// <c>192.168.1.1</c>, ...) is syntactically indistinguishable from a hostname to that same
    /// pattern (digits and dots are both allowed there too), so it needs an explicit negative
    /// lookahead: four dot-separated, all-digit groups, with the optional leading dot accounted for
    /// the same way the rest of the pattern does, is refused regardless of whether each octet is
    /// actually in the valid 0-255 range -- this option cares whether a value LOOKS like an IP
    /// literal, not whether it is a well-formed one.
    ///
    /// CODE_REVIEW gate fourth pass, LOW -- the dotted-quad lookahead above only catches the
    /// canonical four-octet form. Browsers (and .NET's own IPv4 parsers, in some code paths) also
    /// recognise several shorthand/alternate-base forms as the same IP literal: octal
    /// (<c>0177.0.0.1</c>), hex (<c>0x7f.0.0.1</c> or a single bare label like <c>0x7f000001</c>),
    /// shorthand (<c>127.1</c>, <c>127.0.1</c>, fewer than four labels with the trailing one
    /// absorbing the missing octets) and a bare 32-bit integer (<c>2130706433</c>). Enumerating
    /// every one of those forms as its own lookahead would be fragile and incomplete (there is no
    /// finite list -- e.g. mixed octal/hex/decimal octets are also accepted by some parsers).
    /// Simplest robust rule instead: a real DNS TLD is never all-digits and never begins with
    /// <c>0x</c>/<c>0X</c> (an IANA rule -- see the root zone), so rejecting the value whenever its
    /// LAST dot-separated label (or the whole value, when it has none) is all-digits or begins with
    /// <c>0x</c>/<c>0X</c> catches every shorthand/octal/hex/integer form above -- each one's final
    /// label is either a plain decimal octet (all-digit) or, for the single-label bare-hex form, the
    /// whole value itself begins with <c>0x</c>. This only inspects the LAST label, so a digit- or
    /// <c>0x</c>-prefixed label anywhere else (e.g. <c>0x123.example.com</c>) is unaffected -- a real
    /// hostname can have digits in any non-final label already (<c>3host.example.com</c>).
    /// </summary>
    [RegularExpression(
        @"^$|^(?!\.?(\d{1,3}\.){3}\d{1,3}$)(?!.*\.\d+$)(?!^\.?\d+$)(?!.*\.0[xX][a-zA-Z0-9-]*$)(?!^\.?0[xX][a-zA-Z0-9-]*$)\.?[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$",
        ErrorMessage = "Account:CsrfCookieDomain must be empty (host-only) or a bare hostname, e.g. \".example.com\" -- no scheme, port, path, whitespace or IP literal (including octal, hex, shorthand or bare-integer forms).")]
    public string? CsrfCookieDomain { get; set; }
}
