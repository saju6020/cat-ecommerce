using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-ACC-001 §8.2 / ADR-026 / OQ-1. Bangladesh <c>+880</c> policy -- the only mobile normaliser
/// in the solution. Accepts <c>01XXXXXXXXX</c>, <c>+8801XXXXXXXXX</c> or <c>8801XXXXXXXXX</c>
/// (with an optional leading international <c>00</c>, spaces, hyphens or parentheses), always
/// normalising to <c>+8801XXXXXXXXX</c>. Record equality over the normalised value means
/// <c>01712345678</c> and <c>+880 1712-345678</c> are the same person (U-A1).
/// </summary>
public sealed record MobileNumber
{
    public const int MaxLength = 20;

    public string Value { get; }

    private MobileNumber(string value) => Value = value;

    public static MobileNumber Create(string? raw)
    {
        var cleaned = Strip(raw);
        if (cleaned is null) throw Invalid();

        var hasPlus = cleaned.StartsWith('+');
        var digits = hasPlus ? cleaned[1..] : cleaned;

        if (digits.Length == 0 || !digits.All(char.IsDigit)) throw Invalid();

        if (!hasPlus && digits.StartsWith("00"))
            digits = digits[2..];

        string local;
        if (digits.Length == 13 && digits.StartsWith("880"))
            local = "0" + digits[3..];
        else if (digits.Length == 11 && digits.StartsWith("01"))
            local = digits;
        else
            throw Invalid();

        if (local.Length != 11 || !local.StartsWith("01")) throw Invalid();

        var normalised = "+880" + local[1..];
        if (normalised.Length > MaxLength) throw Invalid();

        return new MobileNumber(normalised);
    }

    private static string? Strip(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return new string(raw.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '(' && c != ')').ToArray());
    }

    private static DomainRuleViolationException Invalid() => new(
        BusinessViolationCodes.InvalidMobileNumber,
        "This does not look like a valid mobile number.",
        nameof(MobileNumber));
}
