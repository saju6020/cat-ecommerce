using System.Text.RegularExpressions;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// Gate fix 2026-09-26 (SEC LOW). Format for administrative codes (branch, tax class, clinic
/// service): ASCII letters and digits, with an optional internal hyphen or underscore separator --
/// no leading, trailing or doubled separator, and no NUL byte, whitespace or Unicode lookalike.
/// Checked in a FluentValidation rule, before any uniqueness pre-check query runs, for the same
/// reason as <see cref="SlugFormat"/>. Deliberately case-insensitive (unlike <see cref="SlugFormat"/>),
/// matching the uppercase-alphanumeric codes already in use (e.g. a branch code such as
/// <c>T1A2B3C4</c>).
///
/// Gate fix 2026-09-26 (SEC LOW, hardening round): anchored with <c>\A…\z</c>, not <c>^…$</c> --
/// see <see cref="SlugFormat"/>'s doc comment for why <c>$</c> alone lets a trailing <c>\n</c>
/// through.
/// </summary>
public static partial class CodeFormat
{
    private static readonly Regex Pattern = MyRegex();

    public static bool IsValid(string value) => !string.IsNullOrWhiteSpace(value) && Pattern.IsMatch(value);

    [GeneratedRegex(@"\A[A-Za-z0-9]+(?:[-_][A-Za-z0-9]+)*\z")]
    private static partial Regex MyRegex();
}
