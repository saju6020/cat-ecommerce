namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SHP-001 section 8.13 rule 1 -- pure. Both texts are normalised (lower-case, punctuation to
/// space, collapsed). The allergen matches when its token sequence appears in the ingredient list as
/// WHOLE tokens, where two tokens are the same word when they are equal or differ only by a plural
/// suffix in EITHER direction (<c>s</c>, <c>es</c>, <c>ies</c>/<c>y</c> and <c>ves</c>/<c>f</c>/<c>fe</c>): "Chicken" matches "chickens", "Eggs",
/// "Peas" and "Oats" match "egg", "pea" and "oat", and "anchovy"/"berry"/"leaf" match "anchovies"/"berries"/"leaves"
/// (and the reverse; row 39). "chickpea" is not "chicken", and
/// the allergen "fish" matches the ingredient list "Fish oil". A false positive costs a
/// recommendation, a false negative could cost a cat health, so plural handling is symmetric (this
/// supersedes the one-way rule the section originally documented -- integrator decision 2026-10-04,
/// pending owner confirmation). An ingredient list that is unknown (empty) is NOT a match here; the
/// recommendation policy treats it as possibly containing the allergen (<see cref="IsUnknown"/>).
/// </summary>
public static class AllergenMatcher
{
    public static bool IsUnknown(string? ingredientList) => string.IsNullOrWhiteSpace(ingredientList);

    public static bool Matches(string? allergen, string? ingredientList)
    {
        var allergenTokens = Tokenise(allergen);
        var ingredientTokens = Tokenise(ingredientList);
        if (allergenTokens.Length == 0 || ingredientTokens.Length < allergenTokens.Length) return false;

        for (var start = 0; start <= ingredientTokens.Length - allergenTokens.Length; start++)
        {
            var allMatch = true;
            for (var i = 0; i < allergenTokens.Length && allMatch; i++)
                allMatch = SameWord(allergenTokens[i], ingredientTokens[start + i]);

            if (allMatch) return true;
        }

        return false;
    }

    private static bool SameWord(string a, string b) =>
        a == b || SingularForms(a).Contains(b) || SingularForms(b).Contains(a);

    /// <summary>The words a plural could be the plural OF. Over-generating is deliberate (a false positive
    /// costs a recommendation, a false negative a cat's health); "ss" words keep their own spelling.</summary>
    private static HashSet<string> SingularForms(string word)
    {
        var forms = new HashSet<string>();
        if (word.Length > 3 && word.EndsWith("ies", StringComparison.Ordinal))
            forms.Add(word[..^3] + "y");
        if (word.Length > 3 && word.EndsWith("ves", StringComparison.Ordinal))
        {
            forms.Add(word[..^3] + "f");
            forms.Add(word[..^3] + "fe");
        }
        if (word.Length > 2 && word.EndsWith("es", StringComparison.Ordinal))
            forms.Add(word[..^2]);
        if (word.Length > 1 && word.EndsWith("s", StringComparison.Ordinal) && !word.EndsWith("ss", StringComparison.Ordinal))
            forms.Add(word[..^1]);
        return forms;
    }

    private static string[] Tokenise(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : new string(text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
