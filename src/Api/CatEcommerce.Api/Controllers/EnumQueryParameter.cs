using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// Gate fix 2026-09-26. An optional enum query parameter (banner placement, audit category, leave
/// status, content kind) must answer <c>400</c> on an unrecognised value -- <c>Enumeration
/// .FromDisplayName</c> throws on no match, and a query handler has no <c>ValidationResult</c> to
/// return one through, so the exception used to reach <c>SafeExceptionHandlerMiddleware</c> as an
/// unhandled <c>500</c>. Case-insensitive by <see cref="Enumeration.DisplayName"/>, matching
/// <c>Enumeration</c>'s own shape; an absent/blank value is valid (no filter).
///
/// Gate fix 2026-09-26 (hardening round): delegates to <see cref="EnumerationParsing"/>, the one
/// shared implementation of this lookup, also used by the request-body side (Application's
/// <c>MustBeValidEnumeration</c> FluentValidation rule and <c>AdministrationMapper</c>).
/// </summary>
internal static class EnumQueryParameter
{
    public static bool TryParse<T>(string? value, out T? result) where T : Enumeration => EnumerationParsing.TryParse(value, out result);
}
