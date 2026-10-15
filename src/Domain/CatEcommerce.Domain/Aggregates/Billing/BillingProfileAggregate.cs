using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 §8.1 (NFR-LEG-3) -- the seller's legal identity at one branch; <c>Id == BranchId</c>.
/// Copied onto an invoice at issue (<c>SellerSnapshot</c>), so changing it never alters an issued invoice.
/// Created on first write.
/// </summary>
public sealed class BillingProfileAggregate : AggregateRoot
{
    private readonly List<string> _addressLines = [];
    private readonly List<BillingBankAccount> _bankAccounts = [];

    public string LegalName { get; private set; } = default!;
    public string TaxRegistrationNumber { get; private set; } = default!;
    public IReadOnlyList<string> AddressLines => _addressLines;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? InvoiceFooter { get; private set; }
    public IReadOnlyList<BillingBankAccount> BankAccounts => _bankAccounts;

    private BillingProfileAggregate() { }

    public BillingProfileAggregate(Guid id) : base(id) { }

    public void Open(Platform.Infrastructure.Common.Security.UserContext userContext) => SetDefaultValue(userContext);

    public void Set(BillingProfileSetDto dto)
    {
        Require(dto.LegalName, 200, nameof(dto.LegalName));
        Require(dto.TaxRegistrationNumber, 50, nameof(dto.TaxRegistrationNumber), freeText: false); // a registration number is a legal identifier, not free text

        foreach (var line in dto.AddressLines) BillingFreeText.RefuseCardNumber(line, nameof(dto.AddressLines));

        if (dto.AddressLines.Count is < 1 or > 5 || dto.AddressLines.Any(l => string.IsNullOrWhiteSpace(l) || l.Length > 200))
            throw new DomainRuleViolationException(BusinessViolationCodes.NameRequired, "An address needs one to five lines of up to 200 characters.", nameof(dto.AddressLines));

        if (dto.BankAccounts.Count > BillingRules.MaxBankAccounts)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidSettingValue, $"At most {BillingRules.MaxBankAccounts} bank accounts can be listed.", nameof(dto.BankAccounts));

        foreach (var account in dto.BankAccounts)
        {
            Require(account.BankName, 100, nameof(account.BankName));
            Require(account.AccountName, 100, nameof(account.AccountName));
            Require(account.AccountNumber, 50, nameof(account.AccountNumber), freeText: false); // a bank account number may legitimately be 13-19 digits
            Optional(account.BranchName, 100, nameof(account.BranchName));
            Optional(account.RoutingNumber, 30, nameof(account.RoutingNumber), freeText: false);
        }

        Optional(dto.Phone, 30, nameof(dto.Phone));
        Optional(dto.Email, 200, nameof(dto.Email));
        Optional(dto.InvoiceFooter, 500, nameof(dto.InvoiceFooter));

        LegalName = dto.LegalName.Trim();
        TaxRegistrationNumber = dto.TaxRegistrationNumber.Trim();
        _addressLines.Clear();
        _addressLines.AddRange(dto.AddressLines.Select(l => l.Trim()));
        Phone = dto.Phone?.Trim();
        Email = dto.Email?.Trim();
        InvoiceFooter = dto.InvoiceFooter?.Trim();

        _bankAccounts.Clear();
        var order = 0;
        foreach (var account in dto.BankAccounts)
        {
            _bankAccounts.Add(new BillingBankAccount(Guid.NewGuid(), account.BankName.Trim(), account.AccountName.Trim(), account.AccountNumber.Trim(),
                account.BranchName?.Trim(), account.RoutingNumber?.Trim(), order++));
        }

        AddDomainEvent(new BillingProfileChangedEvent { BranchId = Id, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    private static void Require(string? value, int max, string property, bool freeText = true)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max)
            throw new DomainRuleViolationException(BusinessViolationCodes.NameRequired, $"{property} is required and cannot exceed {max} characters.", property);
        if (freeText) BillingFreeText.RefuseCardNumber(value, property);
    }

    private static void Optional(string? value, int max, string property, bool freeText = true)
    {
        if (value is not null && value.Length > max)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidSettingValue, $"{property} cannot exceed {max} characters.", property);
        if (freeText) BillingFreeText.RefuseCardNumber(value, property);
    }
}
