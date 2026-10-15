using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Order;

/// <summary>
/// HO-ORD-001 §8.14 / §20.8, US-ORD-19 AC1 -- the shop's bank account that bank-transfer customers pay into: a <b>singleton</b> master record
/// (one fixed id), data entered by an administrator -- <b>never configuration</b>, so the account number is not in a committed file (NFR-SEC-7).
/// It is shown to the customer on the order page and in the awaiting-transfer message. An inactive or absent account switches the
/// <c>BankTransfer</c> payment option off (<c>BankTransferDisabled</c>). No card number is ever held here (C-5).
/// </summary>
public sealed class BankTransferAccountAggregate : AggregateRoot
{
    /// <summary>The one account's id: the table can never hold two.</summary>
    public static readonly Guid SingletonId = new("0b9a6f42-6a0e-4b1b-9a39-5d2f6c1f4a01");

    public string AccountName { get; private set; } = default!;
    public string AccountNumber { get; private set; } = default!;
    public string BankName { get; private set; } = default!;
    public string BranchName { get; private set; } = default!;
    public string? RoutingNumber { get; private set; }
    public bool IsActive { get; private set; }

    private BankTransferAccountAggregate() { }

    public BankTransferAccountAggregate(Guid id) : base(id) { }

    public void Create(string accountName, string accountNumber, string bankName, string branchName, string? routingNumber, bool isActive, UserContext userContext)
    {
        Apply(accountName, accountNumber, bankName, branchName, routingNumber, isActive);
        SetDefaultValue(userContext);
        UpdateVersion();
    }

    public void Update(string accountName, string accountNumber, string bankName, string branchName, string? routingNumber, bool isActive)
    {
        Apply(accountName, accountNumber, bankName, branchName, routingNumber, isActive);
        UpdateVersion();
    }

    private void Apply(string accountName, string accountNumber, string bankName, string branchName, string? routingNumber, bool isActive)
    {
        AccountName = Required(accountName, 200, nameof(AccountName));
        AccountNumber = Required(accountNumber, 50, nameof(AccountNumber));
        BankName = Required(bankName, 200, nameof(BankName));
        BranchName = Required(branchName, 200, nameof(BranchName));
        RoutingNumber = string.IsNullOrWhiteSpace(routingNumber) ? null : Required(routingNumber, 50, nameof(RoutingNumber));
        IsActive = isActive;
    }

    private static string Required(string value, int maxLength, string property)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBankTransferAccount, $"{property} is required (up to {maxLength} characters).", property);

        return value.Trim();
    }
}
