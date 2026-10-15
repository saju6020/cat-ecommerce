using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 §8.9 (FR-BIL-5, FR-ORD-17) -- money <b>out</b>: cash or a bank transfer paid back to the customer, drawn from an accepted
/// payment's unallocated amount (the handler's posting also calls <c>PaymentAggregate.RecordRefund</c> in the same save). The refund
/// record -- method, amount, reference, who, why, and (through the credit note) which invoice -- is complete here. Append-only: one mutation,
/// <see cref="Issue"/>; the database refuses any later change (ADR-180). The reference is a bank transfer's (required) and never a card
/// number (C-5). Raises <see cref="RefundIssuedEvent"/>.
/// </summary>
public sealed class RefundAggregate : AggregateRoot
{
    public Guid PaymentId { get; private set; }
    public Guid? CreditNoteId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid CustomerId { get; private set; }
    public RefundMethod Method { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public string? Reference { get; private set; }
    public string Reason { get; private set; } = default!;
    public Guid IssuedBy { get; private set; }
    public DateTimeOffset IssuedOn { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public string? IdempotencyKey { get; private set; }

    private RefundAggregate() { }

    public RefundAggregate(Guid id) : base(id) { }

    public void Issue(RefundIssueDto dto)
    {
        if (dto.Amount <= 0m || decimal.Round(dto.Amount, 2) != dto.Amount || dto.Amount > BillingRules.MaxAmount)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "A refund must be greater than zero with at most two decimals.", nameof(dto.Amount));

        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > BillingRules.MaxReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReasonRequired, "A reason is required.", nameof(dto.Reason));

        var reference = string.IsNullOrWhiteSpace(dto.Reference) ? null : dto.Reference.Trim();
        if (reference is { Length: > BillingRules.MaxReferenceLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidSettingValue, $"A reference cannot exceed {BillingRules.MaxReferenceLength} characters.", nameof(dto.Reference));

        // C-5: a card number is never recorded -- and never echoed back or logged.
        if (CardNumberGuard.LooksLikeCardNumber(reference) || CardNumberGuard.LooksLikeCardNumber(dto.Reason))
            throw new DomainRuleViolationException(BusinessViolationCodes.ReferenceLooksLikeCardNumber, BillingMessages.ReferenceLooksLikeCardNumber, nameof(dto.Reference));

        if (dto.Method == RefundMethod.BankTransfer && reference is null)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReferenceRequired, "A transfer reference is required for a refund by bank transfer.", nameof(dto.Reference));

        PaymentId = dto.PaymentId;
        CreditNoteId = dto.CreditNoteId;
        BranchId = dto.BranchId;
        CustomerId = dto.CustomerId;
        Method = dto.Method;
        Amount = dto.Amount;
        CurrencyCode = dto.CurrencyCode;
        Reference = reference;
        Reason = dto.Reason.Trim();
        IssuedBy = dto.Actor.UserId;
        IssuedOn = dto.Now;
        BusinessDate = dto.BusinessDate;
        IdempotencyKey = dto.IdempotencyKey;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new RefundIssuedEvent
        {
            RefundId = Id, PaymentId = PaymentId, CreditNoteId = CreditNoteId, CustomerId = CustomerId, Amount = Amount,
            CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext,
        });
        UpdateVersion();
    }
}
