using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Billing;

/// <summary>
/// HO-BIL-001 §11.6 -- the <b>decision</b> audit trail. The money-moving decisions carry a reason that must be kept (why a payment was
/// rejected or reversed, why a deposit was released) but a domain event may carry no string (§11.5, U-X1), so the reason lives
/// here: the handler loads the aggregate by id and writes a <c>BusinessEvent</c> audit row holding the reason and the actor (the
/// HO-ADM-001 §11.6 <c>AuditLeaveDecisionHandler</c> pattern). The data change itself is audited by HO-ACC-001's interceptor in the
/// same transaction; this adds the <i>why</i>. Never enqueues a message.
/// </summary>
public sealed class AuditBillingDecisionHandler(
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IAggregateRootRepository<PaymentAggregate> paymentRepository,
    IAggregateRootRepository<CreditNoteAggregate> creditNoteRepository,
    IAggregateRootRepository<RefundAggregate> refundRepository,
    IAggregateRootRepository<CashUpAggregate> cashUpRepository,
    IAggregateRootRepository<BankMatchAggregate> bankMatchRepository,
    IAggregateRootRepository<AccountingExportRunAggregate> exportRunRepository,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<PaymentApprovalWaivedEvent>, IEventHandlerAsync<PaymentApprovedEvent>, IEventHandlerAsync<PaymentRejectedEvent>, IEventHandlerAsync<PaymentReversedEvent>,
      IEventHandlerAsync<PaymentEarmarkReleasedEvent>, IEventHandlerAsync<CreditNoteIssuedEvent>, IEventHandlerAsync<RefundIssuedEvent>, IEventHandlerAsync<CashUpClosedEvent>, IEventHandlerAsync<BankLineUnmatchedEvent>, IEventHandlerAsync<AccountingExportRecordedEvent>
{
    public async Task HandleAsync(PaymentApprovalWaivedEvent @event)
    {
        var payment = await paymentRepository.GetByIdAsync(@event.PaymentId);
        await RecordAsync("PaymentAggregate", @event.PaymentId, "PaymentApprovalWaived",
            payment is null ? null : $"Reason={payment.ApprovalWaiverReason}; Method={payment.Method.DisplayName}; Amount={payment.Amount}", @event.UserContext, @event.CorrelationId);
    }

    public async Task HandleAsync(PaymentApprovedEvent @event)
    {
        var payment = await paymentRepository.GetByIdAsync(@event.PaymentId);
        await RecordAsync("PaymentAggregate", @event.PaymentId, "PaymentApproved", payment is null ? null : $"ApprovedBy={payment.ApprovedBy}", @event.UserContext, @event.CorrelationId);
    }

    public async Task HandleAsync(PaymentRejectedEvent @event)
    {
        var payment = await paymentRepository.GetByIdAsync(@event.PaymentId);
        await RecordAsync("PaymentAggregate", @event.PaymentId, "PaymentRejected", payment?.RejectionReason, @event.UserContext, @event.CorrelationId);
    }

    public async Task HandleAsync(PaymentReversedEvent @event)
    {
        var payment = await paymentRepository.GetByIdAsync(@event.PaymentId);
        await RecordAsync("PaymentAggregate", @event.PaymentId, "PaymentReversed", payment?.ReversalReason, @event.UserContext, @event.CorrelationId);
    }

    public async Task HandleAsync(PaymentEarmarkReleasedEvent @event)
    {
        var payment = await paymentRepository.GetByIdAsync(@event.PaymentId);
        await RecordAsync("PaymentAggregate", @event.PaymentId, "PaymentEarmarkReleased", payment?.Earmark?.ReleaseReason, @event.UserContext, @event.CorrelationId);
    }

    public async Task HandleAsync(CreditNoteIssuedEvent @event)
    {
        var note = await creditNoteRepository.GetByIdAsync(@event.CreditNoteId);
        await RecordAsync("CreditNoteAggregate", @event.CreditNoteId, "CreditNoteIssued", note is null ? null : $"Kind={note.Kind.DisplayName}; Reason={note.Reason}", @event.UserContext, @event.CorrelationId);
    }

    public async Task HandleAsync(RefundIssuedEvent @event)
    {
        var refund = await refundRepository.GetByIdAsync(@event.RefundId);
        await RecordAsync("RefundAggregate", @event.RefundId, "RefundIssued", refund is null ? null : $"Method={refund.Method.DisplayName}; Reason={refund.Reason}", @event.UserContext, @event.CorrelationId);
    }

    public async Task HandleAsync(CashUpClosedEvent @event)
    {
        var cashUp = await cashUpRepository.GetByIdAsync(@event.CashUpId);
        await RecordAsync("CashUpAggregate", @event.CashUpId, "CashUpClosed",
            cashUp is null ? null : $"Date={cashUp.BusinessDate:yyyy-MM-dd}; Variance={cashUp.Variance}; Explanation={cashUp.Explanation}", @event.UserContext, @event.CorrelationId);
    }

    public async Task HandleAsync(BankLineUnmatchedEvent @event)
    {
        var match = await bankMatchRepository.GetByIdAsync(@event.MatchId);
        await RecordAsync("BankMatchAggregate", @event.MatchId, "BankLineUnmatched", match?.UnmatchReason, @event.UserContext, @event.CorrelationId);
    }

    public async Task HandleAsync(AccountingExportRecordedEvent @event)
    {
        var run = await exportRunRepository.GetByIdAsync(@event.RunId);
        await RecordAsync("AccountingExportRunAggregate", @event.RunId, "AccountingExportRun",
            run is null ? null : $"From={run.From:yyyy-MM-dd}; To={run.To:yyyy-MM-dd}; Branch={run.BranchId?.ToString() ?? "all"}; Sha256={run.Sha256}", @event.UserContext, @event.CorrelationId);
    }

    /// <summary>Shared with the later Billing decisions (credit note, refund, cash-up close, bank unmatch).</summary>
    internal async Task RecordAsync(string entityType, Guid entityId, string action, string? after, UserContext userContext, Guid correlationId)
    {
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: entityType, EntityId: entityId, Action: action, Reason: action, ActorUserId: userContext.UserId, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: null, AfterValue: after, UserContext: userContext, CorrelationId: correlationId, Category: AuditCategory.BusinessEvent,
            IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }
}
