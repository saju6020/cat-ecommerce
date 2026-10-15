using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 §8.9 (US-BIL-05 AC2, ADR-186) -- the only way to correct an issued invoice: a numbered document of its own (its own
/// gapless series), crediting lines at the invoice line's <b>frozen</b> rate. <b>Append-only</b>: it has one mutation, <see cref="Issue"/>
/// (and <see cref="Renumber"/> for the numbering retry before it is ever saved); nothing changes it afterwards, and the database refuses
/// it too (ADR-180). What the credit does to the invoice and to the payments applied to it is a posting (ADR-184) the handler commits
/// in the same save. Raises <see cref="CreditNoteIssuedEvent"/>.
/// </summary>
public sealed class CreditNoteAggregate : AggregateRoot
{
    private readonly List<CreditNoteLine> _lines = [];

    public Guid InvoiceId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public string Number { get; private set; } = default!;
    public string FinancialYear { get; private set; } = default!;
    public long SequenceNumber { get; private set; }
    public CreditNoteKind Kind { get; private set; } = default!;
    public string Reason { get; private set; } = default!;
    public IReadOnlyList<CreditNoteLine> Lines => _lines;
    public decimal NetTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal Total { get; private set; }

    /// <summary>The part of the credit that had already been paid and now sits as the customer's credit (FR-ORD-17: "refund as store credit").</summary>
    public decimal ReleasedToCustomerCredit { get; private set; }
    public DateTimeOffset IssuedOn { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public Guid IssuedBy { get; private set; }
    public string? IdempotencyKey { get; private set; }

    private CreditNoteAggregate() { }

    public CreditNoteAggregate(Guid id) : base(id) { }

    public void Issue(CreditNoteIssueDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > BillingRules.MaxReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReasonRequired, "A reason is required.", nameof(dto.Reason));
        BillingFreeText.RefuseCardNumber(dto.Reason, nameof(dto.Reason));

        if (dto.Lines.Count == 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.NothingToCredit, "There is nothing left to credit on this invoice.", nameof(dto.Lines));

        if (dto.Lines.Count > BillingRules.MaxCreditNoteLines)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvoiceTooManyLines, $"A credit note can have at most {BillingRules.MaxCreditNoteLines} lines.", nameof(dto.Lines));

        if (dto.Lines.Any(l => l.GrossAmount <= 0m))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "Every credited line must be greater than zero.", nameof(dto.Lines));

        InvoiceId = dto.InvoiceId;
        BranchId = dto.BranchId;
        CustomerId = dto.CustomerId;
        CurrencyCode = dto.CurrencyCode;
        Number = dto.Number;
        FinancialYear = dto.FinancialYear;
        SequenceNumber = dto.SequenceNumber;
        Kind = dto.Kind;
        Reason = dto.Reason.Trim();

        var order = 0;
        foreach (var line in dto.Lines)
        {
            _lines.Add(new CreditNoteLine(
                Guid.NewGuid(), line.InvoiceLineId, line.Description, line.Quantity, line.RequestedAmount, line.NetAmount, line.TaxRatePercent,
                line.TaxAmount, line.GrossAmount, order++));
        }

        NetTotal = _lines.Sum(l => l.NetAmount);
        TaxTotal = _lines.Sum(l => l.TaxAmount);
        Total = _lines.Sum(l => l.GrossAmount);
        ReleasedToCustomerCredit = dto.ReleasedToCustomerCredit;
        IssuedOn = dto.Now;
        IssueDate = dto.IssueDate;
        IssuedBy = dto.Actor.UserId;
        IdempotencyKey = dto.IdempotencyKey;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new CreditNoteIssuedEvent
        {
            CreditNoteId = Id, InvoiceId = InvoiceId, Total = Total, ReleasedToCustomerCredit = ReleasedToCustomerCredit,
            CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext,
        });
        UpdateVersion();
    }

    /// <summary>Only after a numbering collision on the series row (ADR-179): issued in memory, not yet saved.</summary>
    public void Renumber(string number, long sequenceNumber)
    {
        Number = number;
        SequenceNumber = sequenceNumber;
    }
}
