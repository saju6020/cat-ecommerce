using System.Text.RegularExpressions;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// Gate fix 2026-09-26 (SEC LOW). The lowercase-kebab format shared by content slugs and policy
/// keys: <c>^[a-z0-9]+(-[a-z0-9]+)*$</c>, at most 80 characters. Checking this shape in a
/// FluentValidation rule, which the platform's dispatch pipeline runs strictly before the handler,
/// keeps a NUL byte (or stray whitespace, or a Unicode lookalike) out of the handler's own
/// uniqueness pre-check query -- Postgres rejects a NUL byte in a query parameter at the protocol
/// level (22021), previously an uncaught 500 raised before the aggregate's own validation, which
/// carries the same regex, ever ran.
///
/// Gate fix 2026-09-26 (SEC LOW, hardening round): anchored with <c>\A…\z</c>, not <c>^…$</c> --
/// in .NET, <c>$</c> also matches just before a final <c>\n</c>, so the old pattern accepted
/// <c>"about\n"</c> and let it reach the database, storing <c>about</c> and <c>about\n</c> as two
/// distinct, visually indistinguishable rows. <c>\A…\z</c> matches only the true start/end of the
/// string, so a trailing newline now fails the format check as the doc comment always claimed it
/// would.
/// </summary>
public static partial class SlugFormat
{
    private static readonly Regex Pattern = MyRegex();

    public static bool IsValid(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 80 && Pattern.IsMatch(value);

    [GeneratedRegex(@"\A[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex MyRegex();
}
