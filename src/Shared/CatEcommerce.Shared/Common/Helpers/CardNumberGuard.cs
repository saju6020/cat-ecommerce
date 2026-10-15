using System.Globalization;
using System.Text.RegularExpressions;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// HO-BIL-001 section 8.8.2 (C-5, ADR-182): no card number, ever. Pure.
///
/// The text is first <b>normalised</b>, keeping one output char per input char so indexes still line up with the original:
/// <list type="bullet">
/// <item>every Unicode decimal digit (Bengali, Arabic-Indic, full-width, and supplementary-plane ones such as mathematical digits) becomes the ASCII digit;
/// a supplementary digit is two chars, written as the digit plus an ignorable filler;</item>
/// <item>every separator becomes a plain space: any whitespace, any Unicode dash (Pd: hyphen, en and em dash, non-breaking hyphen), the minus sign,
/// any format character (Cf: zero-width space and joiner, soft hyphen), any space separator (Zs: NBSP and the others), and <c>. / _ , : ; | * ( ) [ ]</c>;</item>
/// <item>anything else -- a <b>letter</b> included -- stays and ends a run, so letters never join digit groups.</item>
/// </list>
/// Each run of digits and separators is then asked whether some stretch of whole digit groups of it is 13-19 digits that pass Luhn -- so
/// <c>4111 1111 1111 1111</c> is caught however it is spaced, with a stray number in front (<c>ref 9 4111 1111 1111 1111</c>), or with one digit glued to
/// its front (<c>94111111111111111</c>). Two everyday numbers that are not card numbers are recognised and left alone: a Bangladeshi mobile number with
/// its country code, and the number of one of our own documents (<c>INV-DHK001-2627-000123</c>). The caller refuses the value and <b>never logs it</b>.
/// </summary>
public static partial class CardNumberGuard
{
    public const int MinDigits = 13;
    public const int MaxDigits = 19;

    private const char Filler = '￿';

    /// <summary>A maximal run of (normalised) digits, optionally separated by any number of spaces.</summary>
    [GeneratedRegex("[0-9]￿?(?: *[0-9]￿?)*", RegexOptions.CultureInvariant)]
    private static partial Regex DigitRunPattern();

    /// <summary>Our own document numbers: <c>INV-</c> or <c>CN-</c>, a branch code, the financial year as two consecutive two-digit years, a six-digit
    /// sequence. A branch code that ends in digits makes these 13 digits, which is not a card.</summary>
    [GeneratedRegex(@"\b(?:INV|CN)-[A-Za-z]{1,8}[0-9]{0,4}-([0-9]{2})([0-9]{2})-[0-9]{6}\b", RegexOptions.CultureInvariant)]
    private static partial Regex OwnDocumentNumberPattern();

    /// <summary>A Bangladeshi mobile number with the country code: 880, 1, an operator digit 3-9, eight more.</summary>
    [GeneratedRegex("^8801[3-9][0-9]{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex MobileWithCountryCodePattern();

    public static bool LooksLikeCardNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var normalised = Normalise(text, blankOwnDocuments: true);
        foreach (Match match in DigitRunPattern().Matches(normalised))
        {
            if (CardWindows(match.Value).Any()) return true;
        }

        return false;
    }

    /// <summary>Masks every card-looking stretch of digits to its last four (<c>••••1111</c>) -- bank-statement descriptions and references on
    /// import (U-G5). Anything else is left exactly as it was, including the original characters around a masked stretch.</summary>
    public static string MaskCardNumbers(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var normalised = Normalise(text, blankOwnDocuments: true);
        var result = new System.Text.StringBuilder();
        var position = 0;

        foreach (Match match in DigitRunPattern().Matches(normalised))
        {
            foreach (var (start, end, digits) in CardWindows(match.Value))
            {
                var from = match.Index + start;
                result.Append(text, position, from - position).Append("••••").Append(digits[^4..]);
                position = match.Index + end;
            }
        }

        return result.Append(text, position, text.Length - position).ToString();
    }

    private static bool IsSeparator(char c) =>
        char.IsWhiteSpace(c) || c is '.' or '/' or '_' or ',' or ':' or ';' or '|' or '*' or '(' or ')' or '[' or ']' or '−'
        || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.DashPunctuation or UnicodeCategory.Format or UnicodeCategory.SpaceSeparator;

    /// <summary>One char in, one char out. A Unicode decimal digit becomes its ASCII digit (a supplementary-plane one: the digit, then a filler for its
    /// second char); a separator becomes a space; anything else stays, so it ends a run.</summary>
    private static string Normalise(string text, bool blankOwnDocuments)
    {
        var output = new char[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                var digit = CharUnicodeInfo.GetDecimalDigitValue(text, i);
                if (digit >= 0)
                {
                    output[i] = (char)('0' + digit);
                    output[i + 1] = Filler;
                }
                else
                {
                    output[i] = c;
                    output[i + 1] = text[i + 1];
                }

                i++;
                continue;
            }

            var value = CharUnicodeInfo.GetDecimalDigitValue(c);
            output[i] = value >= 0 ? (char)('0' + value) : IsSeparator(c) ? ' ' : c;
        }

        if (blankOwnDocuments)
        {
            foreach (Match document in OwnDocumentNumberPattern().Matches(text))
            {
                // Consecutive two-digit years (2627): a real financial year, not an arbitrary digit string.
                if (int.Parse(document.Groups[2].Value, CultureInfo.InvariantCulture) != (int.Parse(document.Groups[1].Value, CultureInfo.InvariantCulture) + 1) % 100) continue;

                for (var i = document.Index; i < document.Index + document.Length; i++) output[i] = 'x';
            }
        }

        return new string(output);
    }

    /// <summary>The card-like stretches of one normalised run: <b>every</b> window of whole digit groups (or a group less its first digit, at the very
    /// start of the run) that is 13-19 digits and passes Luhn, with overlapping windows merged into one stretch. Taking every window, not just the
    /// leftmost-longest, is what stops a stray number in front of a card (<c>37085610 4003 3371 5286 9561</c>) from forming a chance window that hides
    /// the wrong digits and leaves the card's own groups too short to be seen. A merged stretch ends where its last window ends, so its last four
    /// digits are the card's last four.</summary>
    private static IEnumerable<(int Start, int End, string Digits)> CardWindows(string run)
    {
        var groups = new List<(int Start, int End)>();
        for (var i = 0; i < run.Length;)
        {
            if (run[i] == ' ') { i++; continue; }

            var start = i;
            while (i < run.Length && run[i] != ' ') i++;
            groups.Add((start, i));
        }

        var windows = new List<(int Start, int End)>();
        for (var g = 0; g < groups.Count; g++)
        {
            foreach (var start in StartsOf(run, groups[g], isFirstGroup: g == 0))
            {
                for (var last = g; last < groups.Count; last++)
                {
                    var digits = Digits(run[start..groups[last].End]);
                    if (digits.Length > MaxDigits) break;
                    if (IsCardLike(digits)) windows.Add((start, groups[last].End));
                }
            }
        }

        windows.Sort();
        var from = -1;
        var to = -1;
        foreach (var (start, end) in windows)
        {
            if (from >= 0 && start < to) { to = Math.Max(to, end); continue; }

            if (from >= 0) yield return (from, to, Digits(run[from..to]));
            (from, to) = (start, end);
        }

        if (from >= 0) yield return (from, to, Digits(run[from..to]));
    }

    /// <summary>Where a window may begin in a group: its first digit and, only in the first group of a run, one stray digit later (stepping past the
    /// filler of a supplementary-plane digit, which is two chars).</summary>
    private static IEnumerable<int> StartsOf(string run, (int Start, int End) group, bool isFirstGroup)
    {
        yield return group.Start;
        if (!isFirstGroup) yield break;

        var second = group.Start + (group.Start + 1 < group.End && run[group.Start + 1] == Filler ? 2 : 1);
        if (second < group.End && run[second] != Filler) yield return second;
    }

    private static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());

    private static bool IsCardLike(string digits) =>
        digits.Length is >= MinDigits and <= MaxDigits && PassesLuhn(digits) && !MobileWithCountryCodePattern().IsMatch(digits);

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }
}
