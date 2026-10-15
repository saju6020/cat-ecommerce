using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 section 8.1 / 11.4 C26 (US-BIL-09, ADR-190) -- the record of one accounting export: who ran it, for which range and branch, how many rows
/// of each kind, and the SHA-256 of the bytes handed over. The export itself is a download and is never stored; this record is how an
/// accountant proves that two downloads were the same. <b>Append-only</b>: one mutation, <see cref="Record"/>.
/// </summary>
public sealed class AccountingExportRunAggregate : AggregateRoot
{
    public DateOnly From { get; private set; }
    public DateOnly To { get; private set; }
    public Guid? BranchId { get; private set; }
    public int InvoiceCount { get; private set; }
    public int InvoiceLineCount { get; private set; }
    public int PaymentCount { get; private set; }
    public int AllocationCount { get; private set; }
    public int CreditNoteCount { get; private set; }
    public int RefundCount { get; private set; }
    public string Sha256 { get; private set; } = default!;
    public Guid RunBy { get; private set; }
    public DateTimeOffset RunOn { get; private set; }

    private AccountingExportRunAggregate() { }

    public AccountingExportRunAggregate(Guid id) : base(id) { }

    public void Record(AccountingExportRunDto dto)
    {
        if (dto.From > dto.To)
            throw new DomainRuleViolationException(BusinessViolationCodes.ExportRangeTooLarge, "The range ends before it starts.", nameof(dto.To));

        if (dto.To.DayNumber - dto.From.DayNumber + 1 > BillingRules.MaxExportRangeDays)
            throw new DomainRuleViolationException(BusinessViolationCodes.ExportRangeTooLarge,
                $"An export can cover at most {BillingRules.MaxExportRangeDays} days.", nameof(dto.To));

        if (string.IsNullOrWhiteSpace(dto.Sha256) || dto.Sha256.Length != 64)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "The export could not be fingerprinted.", nameof(dto.Sha256));

        From = dto.From;
        To = dto.To;
        BranchId = dto.BranchId;
        InvoiceCount = dto.InvoiceCount;
        InvoiceLineCount = dto.InvoiceLineCount;
        PaymentCount = dto.PaymentCount;
        AllocationCount = dto.AllocationCount;
        CreditNoteCount = dto.CreditNoteCount;
        RefundCount = dto.RefundCount;
        Sha256 = dto.Sha256.ToLowerInvariant();
        RunBy = dto.Actor.UserId;
        RunOn = dto.Now;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new AccountingExportRecordedEvent
        {
            RunId = Id, From = From, To = To, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext,
        });
        UpdateVersion();
    }
}
