using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>
/// Gate fix 2026-09-26 (SEC LOW). A NUL byte (or any other C0 control character besides
/// tab/LF/CR) is not caught by any existing shape rule (<c>NotEmpty</c>/<c>MaximumLength</c>), and
/// Postgres' <c>text</c> type rejects <c>\u0000</c> outright -- previously reaching the database as
/// an uncaught <c>DbUpdateException</c> (an unhandled 500), for content/policy titles, a policy's
/// change summary, a branch name and the maintenance message. One shared rule, rather than
/// duplicating the character scan in each validator. (Gate fix 2026-09-26, fifth pass, correcting
/// the hardening-round comment here: the API-wide <c>NulByteRejectionFilter</c> is an action filter,
/// so it always runs before this FluentValidation rule ever does -- for a NUL byte specifically, it
/// is this rule that never fires, not the other way round. This rule's remaining, real purpose is
/// every *other* disallowed C0 control character besides tab/LF/CR -- BEL, ESC, VT, FF and so on --
/// which the filter does not check for, since it only scans for <c>\u0000</c>.)
///
/// Gate fix 2026-09-26 (round 4): <see cref="MustBeSlugFormat{T}"/> and
/// <see cref="MustBeCodeFormat{T}"/> apply the same "reject the shape before any uniqueness
/// pre-check query runs" discipline to lowercase-kebab keys/slugs and administrative codes.
///
/// Gate fix 2026-09-26 (hardening round): <see cref="MustBeValidEnumeration{T,TEnum}"/> applies the
/// same case-insensitive lookup <c>EnumQueryParameter</c> uses for query strings to a command
/// body's enum-typed field, so an unknown or wrongly-cased value 400s here instead of throwing its
/// way out of <c>AdministrationMapper</c>'s <c>Enumeration.FromDisplayName</c> as an unhandled 500.
/// </summary>
public static class ValidationRuleExtensions
{
    public static IRuleBuilderOptions<T, string?> NoControlCharacters<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder.Must(value => !ControlCharacters.ContainsDisallowed(value)).WithMessage("must not contain control characters.");

    /// <summary>Gate fix 2026-09-27 (HO-NTF-001 SECURITY/CODE_REVIEW MEDIUM 7): rejects the
    /// notification renderer's own internal substitution sentinels (U+E000/U+E001) and any other
    /// Private-Use/Format code point -- see <see cref="ControlCharacters.ContainsPrivateUseOrFormat"/>.</summary>
    public static IRuleBuilderOptions<T, string?> NoPrivateUseOrFormatCharacters<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder.Must(value => !ControlCharacters.ContainsPrivateUseOrFormat(value)).WithMessage("must not contain a private-use or format character.");

    public static IRuleBuilderOptions<T, string?> MustBeSlugFormat<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder.Must(value => string.IsNullOrEmpty(value) || SlugFormat.IsValid(value))
            .WithMessage("must be lowercase letters, digits and hyphens only, e.g. 'boarding-terms'.");

    public static IRuleBuilderOptions<T, string?> MustBeCodeFormat<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder.Must(value => string.IsNullOrEmpty(value) || CodeFormat.IsValid(value))
            .WithMessage("must contain only letters, digits, hyphens and underscores.");

    public static IRuleBuilderOptions<T, string?> MustBeValidEnumeration<T, TEnum>(this IRuleBuilder<T, string?> ruleBuilder) where TEnum : Enumeration =>
        ruleBuilder.Must(value => EnumerationParsing.TryParse<TEnum>(value, out _))
            .WithMessage($"is not a recognised {typeof(TEnum).Name}.");
}
