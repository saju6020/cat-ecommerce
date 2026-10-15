using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 section 8.10 (US-BIL-12, US-BIL-06 AC3, ADR-188) -- one till count per <c>(branch, business date)</c>. The cashier records the
/// counted cash (as often as needed while the day is <c>Open</c>); the close freezes the expected totals the calculator produced, works
/// out the variance, and refuses a non-zero variance without an explanation. <b>Closed is terminal</b> -- there is no reopen (OQ-12).
/// The id is derived from the pair (<see cref="CashUpIds"/>) so "load or create" cannot create two.
/// </summary>
public sealed class CashUpAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public CashUpStatus Status { get; private set; } = default!;
    public decimal? CountedCash { get; private set; }
    public Guid? CountedBy { get; private set; }
    public DateTimeOffset? CountedOn { get; private set; }
    public decimal? Variance { get; private set; }
    public string? Explanation { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public DateTimeOffset? ClosedOn { get; private set; }
    public CashUpSnapshot? ExpectedSnapshot { get; private set; }

    private CashUpAggregate() { }

    public CashUpAggregate(Guid id) : base(id) { }

    public void Open(Guid branchId, DateOnly businessDate, UserContext userContext)
    {
        BranchId = branchId;
        BusinessDate = businessDate;
        Status = CashUpStatus.Open;

        SetDefaultValue(userContext);
        UpdateVersion();
    }

    /// <summary>The date is today or earlier in branch time; a recount replaces the earlier count.</summary>
    public void RecordCount(decimal countedCash, string? explanation, DateOnly todayLocal, DateTimeOffset now, BillingActor actor)
    {
        RequireOpen();

        if (BusinessDate > todayLocal)
            throw new DomainRuleViolationException(BusinessViolationCodes.CashUpDateInFuture, "A till cannot be counted for a day that has not started.", nameof(BusinessDate));

        if (countedCash < 0m || decimal.Round(countedCash, 2) != countedCash || countedCash > BillingRules.MaxAmount)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "The counted cash must be zero or more, with at most two decimals.", nameof(countedCash));

        var text = string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim();
        if (text is { Length: > BillingRules.MaxReasonLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.ReasonRequired, $"An explanation can be at most {BillingRules.MaxReasonLength} characters.", nameof(explanation));
        BillingFreeText.RefuseCardNumber(text, nameof(explanation));

        CountedCash = countedCash;
        CountedBy = actor.UserId;
        CountedOn = now;
        Explanation = text;

        UpdateVersion();
    }

    /// <summary>Freezes the expected totals, computes the variance and closes. A variance other than zero needs an explanation (US-BIL-12 AC2).</summary>
    public void Close(CashUpSnapshot snapshot, DateTimeOffset now, BillingActor actor)
    {
        RequireOpen();

        if (CountedCash is not { } counted)
            throw new DomainRuleViolationException(BusinessViolationCodes.CountRequired, "Count the cash before closing the day.", nameof(CountedCash));

        var variance = counted - snapshot.ExpectedCash;
        if (variance != 0m && string.IsNullOrWhiteSpace(Explanation))
            throw new DomainRuleViolationException(BusinessViolationCodes.VarianceExplanationRequired, BillingMessages.VarianceExplanationRequired, nameof(Explanation));

        Variance = variance;
        ExpectedSnapshot = snapshot;
        Status = CashUpStatus.Closed;
        ClosedBy = actor.UserId;
        ClosedOn = now;

        AddDomainEvent(new CashUpClosedEvent
        {
            CashUpId = Id, BranchId = BranchId, BusinessDate = BusinessDate, Variance = variance,
            CorrelationId = actor.CorrelationId, UserContext = actor.UserContext,
        });
        UpdateVersion();
    }

    private void RequireOpen()
    {
        if (Status != CashUpStatus.Open)
            throw new DomainRuleViolationException(BusinessViolationCodes.CashUpClosed, "This day is closed and can no longer be changed.", nameof(Status));
    }
}

/// <summary>The cash-up id of a <c>(branch, date)</c> -- deterministic, so the same pair is always the same row.</summary>
public static class CashUpIds
{
    public static Guid For(Guid branchId, DateOnly businessDate)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"cash-up:{branchId:N}:{businessDate:yyyy-MM-dd}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
