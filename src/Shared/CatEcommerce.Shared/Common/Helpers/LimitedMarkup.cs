using System.Text.RegularExpressions;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// HO-ADM-001 §8.8 / ADR-052. Content bodies are markup text, never HTML: paragraphs; <c>## </c> /
/// <c>### </c> headings; <c>- </c> bullet lines; <c>**bold**</c>; <c>[text](url)</c> links with an
/// https or site-relative url. The server never stores or serves HTML -- the client renders this
/// text into elements.
/// </summary>
public static partial class LimitedMarkup
{
    public const int MaxLength = 50_000;

    private static readonly Regex DisallowedTagLike = MyRegex();
    private static readonly Regex LinkPattern = MyRegex1();

    /// <summary>Backlog S5 / HO-ADM-002 SECURITY: a site-relative link is "/" NOT followed by another "/" or
    /// a backslash. "//host" and "/\host" are protocol-relative to a browser, and a tab/CR/LF anywhere
    /// ("/&lt;TAB&gt;/host") is stripped by the URL parser and leaves "//host" -- so those are refused too.</summary>
    private static readonly Regex SiteRelativeLink = SiteRelativeRegex();

    /// <summary>Returns an error code, or <c>null</c> when <paramref name="body"/> is valid.
    /// <c>InvalidContentMarkup</c> for anything that looks like HTML/script; <c>InvalidContentLink</c>
    /// for a link whose url is neither https nor site-relative.</summary>
    public static string? Validate(string? body)
    {
        if (string.IsNullOrEmpty(body)) return null;
        if (body.Length > MaxLength) return BusinessViolationCodes.InvalidContentMarkup;

        // Gate fix 2026-09-26: a NUL byte (or any other C0 control character besides tab/LF/CR)
        // is not caught by the tag-like pattern below, and Postgres' text type rejects \u0000
        // outright -- previously reaching the database as an uncaught DbUpdateException (500).
        // Gate fix 2026-09-26 (ARCH LOW): this scan was duplicated three times; now the one shared
        // predicate.
        if (ControlCharacters.ContainsDisallowed(body)) return BusinessViolationCodes.InvalidContentMarkup;

        if (DisallowedTagLike.IsMatch(body)) return BusinessViolationCodes.InvalidContentMarkup;

        foreach (Match match in LinkPattern.Matches(body))
        {
            var url = match.Groups["url"].Value;
            var isHttps = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            var isSiteRelative = SiteRelativeLink.IsMatch(url) && url.IndexOfAny(['\t', '\r', '\n']) < 0;

            if (!isHttps && !isSiteRelative) return BusinessViolationCodes.InvalidContentLink;
        }

        return null;
    }

    public static bool IsValid(string? body) => Validate(body) is null;

    // <, javascript:, data: anywhere in the text is rejected outright -- there is no legitimate
    // use of any of them in the limited markup grammar above.
    [GeneratedRegex(@"<|javascript:|data:", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex MyRegex();

    [GeneratedRegex(@"\[[^\]]*\]\((?<url>[^)]*)\)")]
    private static partial Regex MyRegex1();

    [GeneratedRegex(@"^/(?![/\\])")]
    private static partial Regex SiteRelativeRegex();
}
