using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-SHP-001 section 10.3 / 8.13 -- the keyword table that maps a recorded chronic-condition text
/// (free text, HO-CAT-001 F34) to the health focus a food product may carry. <b>General guidance
/// pending clinical sign-off (OQ-9)</b>, labelled as such in the UI -- never a diagnosis. A keyword
/// matches a word of the condition text by prefix, so "gastro" finds "gastroenteritis" while "renal"
/// does not find "adrenal".
/// </summary>
public static class HealthFocusKeywords
{
    private static readonly IReadOnlyDictionary<HealthFocus, string[]> Table = new Dictionary<HealthFocus, string[]>
    {
        [HealthFocus.Urinary] = ["urinary", "flutd", "cystitis", "bladder", "struvite"],
        [HealthFocus.Renal] = ["kidney", "renal", "ckd"],
        [HealthFocus.Digestive] = ["digestive", "ibd", "colitis", "gastro"],
        [HealthFocus.SkinAndCoat] = ["dermatitis", "skin", "coat"],
        [HealthFocus.WeightManagement] = ["obese", "obesity", "overweight"],
        [HealthFocus.Dental] = ["dental", "gingivitis"],
        [HealthFocus.Hairball] = ["hairball"],
    };

    /// <summary>Every health focus a condition text maps to (possibly none).</summary>
    public static IReadOnlySet<HealthFocus> FocusesFor(string? conditionText)
    {
        var focuses = new HashSet<HealthFocus>();
        if (string.IsNullOrWhiteSpace(conditionText)) return focuses;

        var words = Tokenise(conditionText);
        foreach (var (focus, keywords) in Table)
        {
            if (words.Any(word => keywords.Any(keyword => word.StartsWith(keyword, StringComparison.Ordinal))))
                focuses.Add(focus);
        }

        return focuses;
    }

    private static string[] Tokenise(string text) =>
        new string(text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
