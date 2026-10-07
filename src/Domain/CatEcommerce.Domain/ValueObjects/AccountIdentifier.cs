namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-ACC-001 §8.2. The one place a login/reset "identifier" string becomes a typed lookup.
/// </summary>
public sealed record AccountIdentifier
{
    public MobileNumber? Mobile { get; }
    public EmailAddress? Email { get; }

    private AccountIdentifier(MobileNumber? mobile, EmailAddress? email)
    {
        Mobile = mobile;
        Email = email;
    }

    /// <summary>Throws <see cref="Exceptions.DomainRuleViolationException"/> via the underlying VO factory on failure.</summary>
    public static AccountIdentifier Parse(string raw) =>
        raw.Contains('@')
            ? new AccountIdentifier(null, EmailAddress.Create(raw))
            : new AccountIdentifier(MobileNumber.Create(raw), null);
}
