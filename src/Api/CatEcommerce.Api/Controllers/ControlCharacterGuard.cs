using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Api.Controllers;

/// <summary>
/// Gate fix 2026-09-26 (SEC LOW). The query-string counterpart of
/// <c>CatEcommerce.Application.CommandHandlers.Validators.ValidationRuleExtensions.NoControlCharacters</c>
/// -- a free-text query filter (the audit log's <c>entityType</c>/<c>action</c>) has no
/// FluentValidation command validator to carry the same rule, so it is checked here instead. A NUL
/// byte or any other C0 control character besides tab/LF/CR previously reached Postgres unfiltered
/// and returned an uncaught 500.
///
/// Gate fix 2026-09-26 (ARCH LOW): delegates to the one shared predicate in Shared, rather than
/// carrying its own copy of the scan.
/// </summary>
internal static class ControlCharacterGuard
{
    public static bool ContainsDisallowedControlCharacter(string? value) => ControlCharacters.ContainsDisallowed(value);
}
