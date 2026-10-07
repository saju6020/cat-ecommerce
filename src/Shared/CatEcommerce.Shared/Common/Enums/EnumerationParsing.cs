namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// Gate fix 2026-09-26 (SEC LOW, hardening round). Case-insensitive lookup by
/// <see cref="Enumeration.DisplayName"/>, shared between the query-string side (Api's
/// <c>EnumQueryParameter</c>) and the request-body side (Application's
/// <c>ValidationRuleExtensions.MustBeValidEnumeration</c> FluentValidation rule, and
/// <c>AdministrationMapper</c>'s own parse, which now calls this instead of
/// <see cref="Enumeration.FromDisplayName{T}"/> directly). <c>FromDisplayName</c> is
/// case-sensitive and throws on no match -- never appropriate for a caller-supplied value: this bug
/// was fixed for query parameters, but a command body's enum-typed field (content kind, banner
/// placement, room kind, closure kind, roster-exception kind, service category, leave kind) went
/// straight to the mapper unchecked, so `"page"` (not `"Page"`) or an unrecognised value reached
/// <c>Enumeration.FromDisplayName</c> as an unhandled 500.
/// </summary>
public static class EnumerationParsing
{
    /// <summary>An absent/blank value is valid (no filter, no value) -- required-ness is a separate
    /// rule (<c>NotEmpty</c>) at both call sites.</summary>
    public static bool TryParse<T>(string? value, out T? result) where T : Enumeration
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = null;
            return true;
        }

        result = Enumeration.GetAll<T>().FirstOrDefault(e => string.Equals(e.DisplayName, value, StringComparison.OrdinalIgnoreCase));
        return result is not null;
    }
}
