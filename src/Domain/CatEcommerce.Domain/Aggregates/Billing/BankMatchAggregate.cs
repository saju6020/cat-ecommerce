using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 section 8.1 / 11.4 C24-C25 (US-BIL-06 AC2, ADR-189) -- one bank-statement credit matched to one bank-transfer payment, 1:1 (OQ-18).
/// A match is its own root so the unique indexes on the <b>active</b> line and the <b>active</b> payment make "matched at most once"
/// true under concurrency; an unmatch keeps the row (who, when, why) and frees both sides.
/// </summary>
public sealed class BankMatchAggregate : AggregateRoot
{
    public Guid StatementId { get; private set; }
    public Guid StatementLineId { get; private set; }
    public Guid PaymentId { get; private set; }
    public BankMatchMethod Method { get; private set; } = default!;
    public Guid MatchedBy { get; private set; }
    public DateTimeOffset MatchedOn { get; private set; }
    public DateTimeOffset? UnmatchedOn { get; private set; }
    public Guid? UnmatchedBy { get; private set; }
    public string? UnmatchReason { get; private set; }

    public bool IsActive => UnmatchedOn is null;

    private BankMatchAggregate() { }

    public BankMatchAggregate(Guid id) : base(id) { }

    /// <summary>The handler has already checked the line is a credit, the payment an accepted transfer and the amounts equal.</summary>
    public void Match(BankMatchDto dto)
    {
        StatementId = dto.StatementId;
        StatementLineId = dto.StatementLineId;
        PaymentId = dto.PaymentId;
        Method = dto.Method;
        MatchedBy = dto.Actor.UserId;
        MatchedOn = dto.Now;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new BankLineMatchedEvent
        {
            MatchId = Id, StatementLineId = StatementLineId, PaymentId = PaymentId, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext,
        });
        UpdateVersion();
    }

    public void Unmatch(string reason, DateTimeOffset now, BillingActor actor)
    {
        if (!IsActive)
            throw new DomainRuleViolationException(BusinessViolationCodes.BankMatchNotFound, "This match has already been undone.", nameof(UnmatchedOn));

        if (string.IsNullOrWhiteSpace(reason) || reason.Length > BillingRules.MaxReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReasonRequired, "A reason is required.", nameof(reason));
        BillingFreeText.RefuseCardNumber(reason, nameof(reason));

        UnmatchedOn = now;
        UnmatchedBy = actor.UserId;
        UnmatchReason = reason.Trim();

        AddDomainEvent(new BankLineUnmatchedEvent { MatchId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }
}
