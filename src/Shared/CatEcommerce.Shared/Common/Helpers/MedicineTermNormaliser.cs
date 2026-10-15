using System.Globalization;
using System.Text;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// HO-PHM-001 §8.6 (ADR-140) -- the one normalisation behind allergy and interaction matching. Lower-case invariant; diacritics stripped; every run of
/// non-letters and non-digits becomes a single space; trimmed; a trailing <c>s</c> is removed from words of five or more letters
/// (<c>penicillins</c> becomes <c>penicillin</c>, <c>gas</c> stays <c>gas</c>, <c>glass</c> stays <c>glass</c>). Pure.
/// </summary>
public static class MedicineTermNormaliser
{
    public const int MinimumSingularisedWordLength = 5;

    public static string Normalise(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsLetterOrDigit(ch))
            {
                if (pendingSpace && builder.Length > 0) builder.Append(' ');
                pendingSpace = false;
                builder.Append(ch);
            }
            else
            {
                pendingSpace = true;
            }
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Singularise));
    }

    private static string Singularise(string word) =>
        word.Length >= MinimumSingularisedWordLength && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal)
            ? word[..^1]
            : word;

    /// <summary>Whether <paramref name="needle"/> occurs in <paramref name="haystack"/> as a run of whole words (both already normalised).</summary>
    public static bool ContainsWholeWords(string haystack, string needle)
    {
        if (needle.Length == 0 || haystack.Length == 0) return false;
        return $" {haystack} ".Contains($" {needle} ", StringComparison.Ordinal);
    }
}
