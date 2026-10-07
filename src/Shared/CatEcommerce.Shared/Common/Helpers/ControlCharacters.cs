using System.Globalization;
using System.Text;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// Gate fix 2026-09-26 (ARCH LOW). The single predicate for "does this text contain a control
/// character that must never reach Postgres or the client" -- previously duplicated three times
/// (Api's <c>ControlCharacterGuard</c>, Application's
/// <c>ValidationRuleExtensions.ContainsDisallowedControlCharacter</c>, and this project's own
/// <see cref="LimitedMarkup"/>'s inline loop). Tab, LF and CR are allowed; every other C0 control
/// character, including NUL, is not -- Postgres' <c>text</c> type rejects <c>\u0000</c> outright,
/// at the protocol level, wherever it appears in a query parameter.
/// </summary>
public static class ControlCharacters
{
    public static bool ContainsDisallowed(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        foreach (var ch in value)
        {
            if (ch < ' ' && ch is not ('\t' or '\n' or '\r')) return true;
        }

        return false;
    }

    /// <summary>
    /// HO-NTF-001 SECURITY/CODE_REVIEW gate fix (2026-09-27, MEDIUM 7). Rejects any Unicode
    /// Private-Use (<c>Co</c>) or Format (<c>Cf</c>) code point -- including the two the
    /// notification renderer's <c>NotificationRenderer</c> uses as its internal, never-user-facing
    /// substitution sentinels (U+E000/U+E001, ADR-062). If an admin-authored template or a
    /// request's own variable value already contains one of these characters as literal text, the
    /// renderer's own sentinel-matching regex cannot tell it apart from a real sentinel it wrote
    /// itself, and misinterprets the following digits as an index into its resolved-value list --
    /// throwing <c>ArgumentOutOfRangeException</c> on every future render of that template
    /// (proven: SECURITY gate probe P6). Scoped to Notifications' own call sites
    /// (<c>TemplateRules</c>, <c>NotificationRequestAggregate.Create</c>) rather than folded into
    /// <see cref="ContainsDisallowed"/> itself, because Private-Use/Format characters are otherwise
    /// harmless to Postgres and to every other area's own text fields.
    /// </summary>
    public static bool ContainsPrivateUseOrFormat(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        foreach (var rune in value.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.PrivateUse or UnicodeCategory.Format) return true;
        }

        return false;
    }
}
