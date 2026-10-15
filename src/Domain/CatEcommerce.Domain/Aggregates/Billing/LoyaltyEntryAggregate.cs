using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 section 8.11 (ADR-191) -- one line of a customer points ledger: <b>append-only</b>, the database refuses any change. Its own root (a customer
/// accumulates entries without bound, bounded-contexts rule 3). The kind says what happened; the invoice, credit note or merged customer it
/// came from is what makes each handler idempotent (a filtered unique index on the source).
/// </summary>
public sealed class LoyaltyEntryAggregate : AggregateRoot
{
    public Guid CustomerId { get; private set; }
    public LoyaltyEntryKind Kind { get; private set; } = default!;
    public int Points { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public Guid? CreditNoteId { get; private set; }
    public Guid? SourceCustomerId { get; private set; }

    /// <summary>For a reversal larger than the balance: the part that could not be taken back (the customer had already spent it).</summary>
    public int Shortfall { get; private set; }

    /// <summary>True when the entry source (the invoice, credit note or merged customer) is its own idempotency key -- accrual, accrual reversal and the
    /// merge transfers happen at most once per source. A redemption can be applied, removed and applied again on the same invoice, so it is not.</summary>
    public bool IdempotentSource { get; private set; }
    public DateTimeOffset OccurredOn { get; private set; }

    private LoyaltyEntryAggregate() { }

    public LoyaltyEntryAggregate(Guid id) : base(id) { }

    public void Record(LoyaltyEntryDto dto)
    {
        if (dto.Points <= 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "Points must be a positive number.", nameof(dto.Points));

        if (dto.Shortfall < 0 || dto.Shortfall > dto.Points)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "The shortfall cannot be negative or larger than the points.", nameof(dto.Shortfall));

        CustomerId = dto.CustomerId;
        Kind = dto.Kind;
        Points = dto.Points;
        InvoiceId = dto.InvoiceId;
        CreditNoteId = dto.CreditNoteId;
        SourceCustomerId = dto.SourceCustomerId;
        Shortfall = dto.Shortfall;
        IdempotentSource = Kind != LoyaltyEntryKind.Redemption && Kind != LoyaltyEntryKind.RedemptionReversal;
        OccurredOn = dto.Now;

        SetDefaultValue(dto.Actor.UserContext);

        if (Kind == LoyaltyEntryKind.Accrual && InvoiceId is { } accruedOn)
            AddDomainEvent(new LoyaltyPointsAccruedEvent
            { CustomerId = CustomerId, InvoiceId = accruedOn, Points = Points, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });

        if (Kind == LoyaltyEntryKind.Redemption && InvoiceId is { } redeemedOn)
            AddDomainEvent(new LoyaltyPointsRedeemedEvent
            { CustomerId = CustomerId, InvoiceId = redeemedOn, Points = Points, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });

        UpdateVersion();
    }
}
