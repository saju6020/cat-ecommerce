using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>HO-ACC-001 §8.2 / ADR-026. Trimmed, lower-cased invariant, exactly one <c>@</c>.</summary>
public sealed record EmailAddress
{
    public const int MaxLength = 256;

    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    public static EmailAddress Create(string? raw)
    {
        var trimmed = raw?.Trim();
        if (string.IsNullOrEmpty(trimmed)) throw Invalid();

        var lower = trimmed.ToLowerInvariant();
        if (lower.Length > MaxLength) throw Invalid();

        var atIndex = lower.IndexOf('@');
        if (atIndex <= 0 || lower.IndexOf('@', atIndex + 1) != -1) throw Invalid();

        var localPart = lower[..atIndex];
        var domain = lower[(atIndex + 1)..];
        if (localPart.Length == 0 || domain.Length == 0) throw Invalid();

        var labels = domain.Split('.');
        if (labels.Length < 2 || labels.Any(string.IsNullOrEmpty)) throw Invalid();

        return new EmailAddress(lower);
    }

    private static DomainRuleViolationException Invalid() => new(
        BusinessViolationCodes.InvalidEmailAddress,
        "This does not look like a valid email address.",
        nameof(EmailAddress));
}
