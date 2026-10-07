using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Product;

/// <summary>
/// HO-SHP-001 section 8.11 -- a back-in-stock alert, one root per customer per SKU request (alerts
/// accumulate without bound, bounded-contexts.md rule 3). <c>Pending</c> becomes <c>Notified</c>
/// (terminal -- a later sell-out and restock sends nothing more, US-SHP-13 AC1) or <c>Closed</c>.
/// "SKU currently out of stock", "on sale" and "one pending alert per customer per SKU" span other
/// aggregates and are the handler's checks (the last also a filtered unique index).
/// </summary>
public sealed class StockAlertAggregate : AggregateRoot
{
    public Guid CustomerId { get; private set; }
    public Guid SkuId { get; private set; }
    public StockAlertStatus Status { get; private set; } = default!;
    public StockAlertClosureReason? ClosureReason { get; private set; }
    public DateTimeOffset RequestedOn { get; private set; }
    public DateTimeOffset? NotifiedOn { get; private set; }

    private StockAlertAggregate() { }

    public StockAlertAggregate(Guid id) : base(id) { }

    public void Request(Guid customerId, Guid skuId, DateTimeOffset now, UserContext userContext, Guid correlationId)
    {
        CustomerId = customerId;
        SkuId = skuId;
        Status = StockAlertStatus.Pending;
        RequestedOn = now;

        SetDefaultValue(userContext);

        AddDomainEvent(new StockAlertRequestedEvent { StockAlertId = Id, SkuId = skuId, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    public void MarkNotified(DateTimeOffset now, UserContext userContext, Guid correlationId)
    {
        EnsurePending("notify");

        Status = StockAlertStatus.Notified;
        NotifiedOn = now;

        AddDomainEvent(new StockAlertNotifiedEvent { StockAlertId = Id, SkuId = SkuId, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    public void Close(StockAlertClosureReason reason, UserContext userContext, Guid correlationId)
    {
        EnsurePending("close");

        Status = StockAlertStatus.Closed;
        ClosureReason = reason;

        AddDomainEvent(new StockAlertClosedEvent { StockAlertId = Id, SkuId = SkuId, Reason = reason.Value, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    /// <summary>Merge (ADR-096): the alert moves to the surviving customer. Idempotent.</summary>
    public bool ReassignCustomer(Guid survivingCustomerId)
    {
        if (CustomerId == survivingCustomerId) return false;

        CustomerId = survivingCustomerId;
        UpdateVersion();
        return true;
    }

    private void EnsurePending(string action)
    {
        if (Status != StockAlertStatus.Pending)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStockAlertTransition,
                $"Cannot {action} an alert that is {Status.DisplayName}.", nameof(Status));
    }
}
