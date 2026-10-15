using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 section 8.1 / 11.4 C22 (US-BIL-06 AC1, ADR-189) -- money a courier handed over for cash-on-delivery collections. Each line says how
/// much of one collection (a courier-collected COD payment) it covers; the shortfall of a collection is what was collected minus
/// everything remitted across all remittances. <b>Append-only</b>: one mutation, <see cref="Record"/>; a mistake is a further
/// remittance, never an edit. Whether each line's payment is a COD collection of this courier, and not over-remitted, is the
/// handler's (it needs the payments); everything the remittance can check on its own is here.
/// </summary>
public sealed class CodRemittanceAggregate : AggregateRoot
{
    private readonly List<RemittanceLine> _lines = [];

    public Guid BranchId { get; private set; }
    public string CourierCode { get; private set; } = default!;
    public DateOnly ReceivedOn { get; private set; }
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public RemittanceMethod Method { get; private set; } = default!;
    public string? Reference { get; private set; }
    public Guid RecordedBy { get; private set; }
    public DateTimeOffset RecordedOn { get; private set; }
    public IReadOnlyList<RemittanceLine> Lines => _lines;

    /// <summary>The part of the received amount no line covers (listed on the reconciliation, not hidden).</summary>
    public decimal Unapplied => Amount - _lines.Sum(l => l.AmountRemitted);

    private CodRemittanceAggregate() { }

    public CodRemittanceAggregate(Guid id) : base(id) { }

    public void Record(Guid branchId, CodRemittanceDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CourierCode) || dto.CourierCode.Trim().Length > BillingRules.MaxCourierCodeLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.CollectorRequired, "Name the courier that handed the money over.", nameof(dto.CourierCode));
        BillingFreeText.RefuseCardNumber(dto.CourierCode, nameof(dto.CourierCode));

        if (dto.Amount <= 0m || decimal.Round(dto.Amount, 2) != dto.Amount || dto.Amount > BillingRules.MaxAmount)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "A remittance must be greater than zero with at most two decimals.", nameof(dto.Amount));

        if (dto.Lines.Count == 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.CollectionNotCod, "Name the collections this remittance covers.", nameof(dto.Lines));

        if (dto.Lines.Count > BillingRules.MaxRemittanceLines)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, $"A remittance can cover at most {BillingRules.MaxRemittanceLines} collections.", nameof(dto.Lines));

        if (dto.Lines.Any(l => l.AmountRemitted <= 0m || decimal.Round(l.AmountRemitted, 2) != l.AmountRemitted))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "Every covered amount must be greater than zero with at most two decimals.", nameof(dto.Lines));

        if (dto.Lines.Select(l => l.PaymentId).Distinct().Count() != dto.Lines.Count)
            throw new DomainRuleViolationException(BusinessViolationCodes.RemittanceExceedsCollection, "A collection can appear only once in a remittance.", nameof(dto.Lines));

        if (dto.Lines.Sum(l => l.AmountRemitted) > dto.Amount)
            throw new DomainRuleViolationException(BusinessViolationCodes.RemittanceExceedsCollection, "The covered amounts add up to more than the money received.", nameof(dto.Lines));

        var reference = string.IsNullOrWhiteSpace(dto.Reference) ? null : dto.Reference.Trim();
        if (reference is { Length: > BillingRules.MaxReferenceLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidSettingValue, $"A reference cannot exceed {BillingRules.MaxReferenceLength} characters.", nameof(dto.Reference));

        if (CardNumberGuard.LooksLikeCardNumber(reference))
            throw new DomainRuleViolationException(BusinessViolationCodes.ReferenceLooksLikeCardNumber, BillingMessages.ReferenceLooksLikeCardNumber, nameof(dto.Reference));

        BranchId = branchId;
        CourierCode = dto.CourierCode.Trim();
        ReceivedOn = dto.ReceivedOn;
        Amount = dto.Amount;
        CurrencyCode = dto.CurrencyCode;
        Method = dto.Method;
        Reference = reference;
        RecordedBy = dto.Actor.UserId;
        RecordedOn = dto.Now;

        foreach (var line in dto.Lines)
            _lines.Add(new RemittanceLine(Guid.NewGuid(), line.PaymentId, line.AmountRemitted));

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new CodRemittanceRecordedEvent
        {
            RemittanceId = Id, Amount = Amount, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext,
        });
        UpdateVersion();
    }
}
