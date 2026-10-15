namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-BIL-001 §8.6 (NFR-LEG-3) -- the seller's legal identity, copied from the branch's
/// <c>BillingProfileAggregate</c> at issue and never re-read, so a later change to the profile never
/// alters an issued invoice. <see cref="BankAccountsJson"/> is the bank-account list serialised once at
/// issue (shown under "how to pay" on the invoice and the portal).
/// </summary>
public sealed record SellerSnapshot
{
    public string LegalName { get; init; }
    public string TaxRegistrationNumber { get; init; }
    public string AddressText { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? InvoiceFooter { get; init; }
    public string BankAccountsJson { get; init; }

    public SellerSnapshot(string legalName, string taxRegistrationNumber, string addressText, string? phone, string? email, string? invoiceFooter, string bankAccountsJson)
    {
        LegalName = legalName;
        TaxRegistrationNumber = taxRegistrationNumber;
        AddressText = addressText;
        Phone = phone;
        Email = email;
        InvoiceFooter = invoiceFooter;
        BankAccountsJson = bankAccountsJson;
    }

    private SellerSnapshot()
    {
        LegalName = default!;
        TaxRegistrationNumber = default!;
        AddressText = default!;
        BankAccountsJson = default!;
    }
}
