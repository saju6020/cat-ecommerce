namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-BIL-001 §8.6 / ADR-192 -- the customer's name, mobile, email and default address text at issue.
/// Retained under D-9 after the account is anonymised: an issued invoice is a financial record and is
/// never rewritten (ADR-180), so this snapshot is the one place a customer's personal data survives
/// inside Billing, and the Billing eraser reports it as <c>Held</c>.
/// </summary>
public sealed record BillToSnapshot
{
    public string FullName { get; init; }
    public string? Mobile { get; init; }
    public string? Email { get; init; }
    public string? AddressText { get; init; }

    public BillToSnapshot(string fullName, string? mobile, string? email, string? addressText)
    {
        FullName = fullName;
        Mobile = mobile;
        Email = email;
        AddressText = addressText;
    }

    private BillToSnapshot() => FullName = default!;
}
