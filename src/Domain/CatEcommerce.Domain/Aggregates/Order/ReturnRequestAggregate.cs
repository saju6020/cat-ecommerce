using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Order;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Order;

/// <summary>
/// HO-ORD-001 §8.10 (ADR-210), US-ORD-13, 14 and US-ORD-20 AC3 -- a return: the customer's request (or a delivery failure's automatic one), its approval by staff,
/// the receipt of the goods into quarantine and the decision, per line, to restock them against their original batch or write them off.
/// <code>Requested -> Approved | Rejected;  Approved -> Received -> Closed</code>
/// A <b>delivery failure</b> return starts <c>Approved</c>, covers every line and is received like any other. Returned stock <b>always</b> enters quarantine
/// first (Inventory's <c>ReceiveReturnAsync</c>); this aggregate records what came back, from which batches, and what staff decided -- never a batch choice.
/// </summary>
public sealed class ReturnRequestAggregate : AggregateRoot
{
    private readonly List<ReturnLine> _lines = [];

    public Guid OrderId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public ReturnKind Kind { get; private set; } = default!;
    public ReturnStatus Status { get; private set; } = default!;
    public Guid RequestedBy { get; private set; }
    public DateTimeOffset RequestedOn { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedOn { get; private set; }
    public string? DecisionNote { get; private set; }
    public Guid? ReceivedBy { get; private set; }
    public DateTimeOffset? ReceivedOn { get; private set; }
    public DateTimeOffset? ClosedOn { get; private set; }

    public IReadOnlyList<ReturnLine> Lines => _lines;

    private ReturnRequestAggregate() { }

    public ReturnRequestAggregate(Guid id) : base(id) { }

    /// <summary>A customer's (or staff on their behalf) request. Every line was already checked by <c>ReturnEligibilityPolicy</c>; here only the shape is.</summary>
    public void Request(ReturnRequestDto dto)
    {
        ValidateLines(dto.Lines);

        OrderId = dto.OrderId;
        CustomerId = dto.CustomerId;
        Kind = dto.Kind;
        RequestedBy = dto.RequestedBy;
        RequestedOn = dto.RequestedOn;
        Status = dto.Kind == ReturnKind.DeliveryFailure ? ReturnStatus.Approved : ReturnStatus.Requested;
        if (Status == ReturnStatus.Approved)
        {
            DecidedBy = dto.RequestedBy;
            DecidedOn = dto.RequestedOn;
        }

        _lines.AddRange(dto.Lines.Select(l => new ReturnLine(Guid.NewGuid(), l.OrderLineId, l.Quantity, l.Reason, l.Condition, Clean(l.CustomerNote), l.RequiresDefectConfirmation)));

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new ReturnRequestedEvent { ReturnRequestId = Id, OrderId = OrderId, Kind = Kind.Value, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        if (Status == ReturnStatus.Approved)
            AddDomainEvent(new ReturnApprovedEvent { ReturnRequestId = Id, OrderId = OrderId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });

        UpdateVersion();
    }

    /// <summary>The refused delivery or collection (US-ORD-20 AC3): an <c>Approved</c> return of every line, in the quantity delivered.</summary>
    public static ReturnRequestAggregate ForDeliveryFailure(Guid id, OrderAggregate order, Guid by, DateTimeOffset now, UserContext user, Guid correlationId)
    {
        var aggregate = new ReturnRequestAggregate(id);
        aggregate.Request(new ReturnRequestDto(
            order.Id, order.CustomerId, ReturnKind.DeliveryFailure,
            order.Lines.Select(l => new ReturnLineDto(l.Id, l.Quantity, ReturnReason.Other, ItemCondition.Sealed, null, false)).ToList(), by, now, user, correlationId));
        return aggregate;
    }

    /// <summary>Staff approve (<c>Requested -> Approved</c>). A line that needs a confirmed defect (BR-10) must carry <c>DefectConfirmed</c> with a note.</summary>
    public void Approve(Guid by, IReadOnlyList<DefectConfirmationDto> defects, string? note, DateTimeOffset now, UserContext user, Guid correlationId)
    {
        EnsureStatus(ReturnStatus.Requested, "approved");

        foreach (var line in _lines)
        {
            var given = defects.FirstOrDefault(d => d.OrderLineId == line.OrderLineId);
            var confirmed = given?.DefectConfirmed ?? false;
            var defectNote = Clean(given?.DefectNote);
            if (line.RequiresDefectConfirmation && (!confirmed || defectNote is null))
                throw new DomainRuleViolationException(BusinessViolationCodes.DefectConfirmationRequired,
                    "This item was opened or used. Confirm the defect and say what it is before approving the return.", $"Lines[{_lines.IndexOf(line)}]");

            line.ConfirmDefect(confirmed, defectNote);
        }

        Status = ReturnStatus.Approved;
        DecidedBy = by;
        DecidedOn = now;
        DecisionNote = Clean(note);
        AddDomainEvent(new ReturnApprovedEvent { ReturnRequestId = Id, OrderId = OrderId, CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    public void Reject(Guid by, string note, DateTimeOffset now, UserContext user, Guid correlationId)
    {
        EnsureStatus(ReturnStatus.Requested, "rejected");
        if (string.IsNullOrWhiteSpace(note) || note.Trim().Length > OrderRules.MaxReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReturnTransition, $"Say why the return is refused (up to {OrderRules.MaxReasonLength} characters).", nameof(note));

        Status = ReturnStatus.Rejected;
        DecidedBy = by;
        DecidedOn = now;
        DecisionNote = note.Trim();
        AddDomainEvent(new ReturnRejectedEvent { ReturnRequestId = Id, OrderId = OrderId, CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    /// <summary>The goods arrived (<c>Approved -> Received</c>): per line, how many (never more than approved) and the original batches they came from.</summary>
    public void Receive(Guid by, IReadOnlyList<ReturnReceiptDto> receipts, DateTimeOffset now, UserContext user, Guid correlationId)
    {
        EnsureStatus(ReturnStatus.Approved, "received");

        foreach (var receipt in receipts)
        {
            var line = _lines.FirstOrDefault(l => l.OrderLineId == receipt.OrderLineId)
                ?? throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReturnTransition, "That order line is not part of this return.", nameof(receipts));

            if (receipt.QuantityReceived < 0 || receipt.QuantityReceived > line.Quantity)
                throw new DomainRuleViolationException(BusinessViolationCodes.ReturnReceiptExceedsApproved, "More items were received than were approved for return.", nameof(receipts));

            line.Receive(receipt.QuantityReceived, receipt.Receipts);
        }

        Status = ReturnStatus.Received;
        ReceivedBy = by;
        ReceivedOn = now;
        AddDomainEvent(new ReturnReceivedEvent { ReturnRequestId = Id, OrderId = OrderId, CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    /// <summary>The caller is anonymised or merged: the return follows the customer (ADR-039).</summary>
    public bool ReassignCustomer(Guid survivingCustomerId)
    {
        if (CustomerId == survivingCustomerId) return false;

        CustomerId = survivingCustomerId;
        UpdateVersion();
        return true;
    }

    private void EnsureStatus(ReturnStatus expected, string verb)
    {
        if (Status != expected)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReturnTransition, $"A return that is {Status.DisplayName.ToLowerInvariant()} cannot be {verb}.", nameof(Status));
    }

    private static void ValidateLines(IReadOnlyList<ReturnLineDto> lines)
    {
        if (lines.Count == 0 || lines.Count > OrderRules.MaxLines)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReturnQuantityExceeded, $"A return has 1 to {OrderRules.MaxLines} lines.", "Lines");

        if (lines.Select(l => l.OrderLineId).Distinct().Count() != lines.Count)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReturnQuantityExceeded, "Each order line can appear once in a return.", "Lines");

        foreach (var line in lines)
        {
            if (line.Quantity < 1)
                throw new DomainRuleViolationException(BusinessViolationCodes.ReturnQuantityExceeded, "Return at least one item.", "Lines");

            if (line.CustomerNote is { Length: > OrderRules.MaxReasonLength })
                throw new DomainRuleViolationException(BusinessViolationCodes.ReturnQuantityExceeded, $"A note is up to {OrderRules.MaxReasonLength} characters.", "Lines");
        }
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
