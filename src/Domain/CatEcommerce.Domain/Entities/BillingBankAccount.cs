namespace CatEcommerce.Domain.Entities;

/// <summary>HO-BIL-001 §8.1 -- one of the branch's own bank accounts (≤ 5), shown under "how to pay".
/// These are the business's own details, not a customer's, so the card-number guard does not apply
/// (§11.4 C1).</summary>
public sealed class BillingBankAccount
{
    public Guid Id { get; private set; }
    public string BankName { get; private set; } = default!;
    public string AccountName { get; private set; } = default!;
    public string AccountNumber { get; private set; } = default!;
    public string? BranchName { get; private set; }
    public string? RoutingNumber { get; private set; }
    public int SortOrder { get; private set; }

    private BillingBankAccount() { }

    public BillingBankAccount(Guid id, string bankName, string accountName, string accountNumber, string? branchName, string? routingNumber, int sortOrder)
    {
        Id = id;
        BankName = bankName;
        AccountName = accountName;
        AccountNumber = accountNumber;
        BranchName = branchName;
        RoutingNumber = routingNumber;
        SortOrder = sortOrder;
    }
}
