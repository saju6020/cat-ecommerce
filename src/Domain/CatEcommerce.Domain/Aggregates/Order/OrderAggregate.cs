using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Order;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Order;

/// <summary>
/// HO-ORD-001 §8.3, §8.4 (ADR-197, ADR-201), US-ORD-08, 09, 12 -- an order: one customer's purchase (or a counter sale), its priced lines with their batch
/// allocations, its fulfilment snapshot, its money and its Appendix C status with the full, attributed history.
///
/// <b>Statuses are exactly Appendix C's</b>; <see cref="OrderTransitionRules"/> holds the one table and every mutation goes through it, so an invalid move
/// is always <c>InvalidOrderTransition</c> naming both states and a valid one appends exactly one <see cref="OrderStatusChange"/> row with the actor.
/// Totals are re-summed from the lines in minor units on <see cref="Place"/>; a mismatch is a programming error (<see cref="InvalidOperationException"/>),
/// never a business outcome. The aggregate holds <b>references</b> to Billing (the invoice, the credit notes, the payments) and to Inventory (the batch
/// allocations); it never holds the money itself and never chooses a batch (ADR-208).
/// </summary>
public sealed class OrderAggregate : AggregateRoot
{
    private readonly List<OrderLine> _lines = [];
    private readonly List<OrderStatusChange> _statusHistory = [];
    private readonly List<OrderPaymentRef> _payments = [];

    public string OrderNumber { get; private set; } = default!;
    public OrderChannel Channel { get; private set; } = default!;
    public Guid? CustomerId { get; private set; }
    public Guid BranchId { get; private set; }
    public FulfilmentMethod FulfilmentMethod { get; private set; } = default!;
    public OrderPaymentMethod PaymentMethod { get; private set; } = default!;
    public OrderStatus Status { get; private set; } = default!;
    public string CurrencyCode { get; private set; } = default!;
    public FulfilmentDetails Fulfilment { get; private set; } = default!;
    public OrderTotals Totals { get; private set; } = default!;

    /// <summary>FU1 ORD-56 (ADR-217): the part of <c>Totals.TaxTotal</c> that is tax on the delivery charge, as quoted at placement. A held transfer confirmed later is invoiced at the rate in force at <see cref="PlacedOn"/>, so the invoice carries this amount.</summary>
    public decimal DeliveryTaxAmount { get; private set; }
    public Guid? DiscountCodeId { get; private set; }
    public string? DiscountCodeText { get; private set; }
    public bool DiscountReleased { get; private set; }
    public string QuoteFingerprint { get; private set; } = default!;
    public Guid? IdempotencyKey { get; private set; }
    public DateTimeOffset PlacedOn { get; private set; }
    public DateTimeOffset? PaymentDueBy { get; private set; }
    public string? CustomerTransferReference { get; private set; }
    public DateTimeOffset? TransferReportedOn { get; private set; }
    public HeldPaymentConfirmation? HeldPayment { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public string? InvoiceNumber { get; private set; }
    public IReadOnlyList<Guid> CreditNoteIds { get; private set; } = [];
    public decimal AmountPaid { get; private set; }
    public decimal AmountRefunded { get; private set; }
    public decimal RefundDue { get; private set; }
    /// <summary>The change handed back on a counter sale (HO-ORD-001 §8.11); the receipt reprints it. Zero for every other order.</summary>
    public decimal ChangeGiven { get; private set; }
    public Shipment? Shipment { get; private set; }
    public DateTimeOffset? ReadyForCollectionOn { get; private set; }
    public DateOnly? CollectByDate { get; private set; }
    public DateTimeOffset? DeliveredOn { get; private set; }
    public Guid? DeliveryRecordedBy { get; private set; }
    public CodCollection? CodCollection { get; private set; }
    public DeliveryFailureReason? DeliveryFailureReason { get; private set; }
    public OrderCancellationReason? CancellationReason { get; private set; }
    public string? CancellationNote { get; private set; }
    public DateTimeOffset? CancelledOn { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public bool CancelledByCustomer { get; private set; }
    public DateTimeOffset? ConfirmedOn { get; private set; }
    public DateTimeOffset? PackingStartedOn { get; private set; }
    public bool CustomerAnonymised { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines;
    public IReadOnlyList<OrderStatusChange> StatusHistory => _statusHistory;
    public IReadOnlyList<OrderPaymentRef> Payments => _payments;

    /// <summary>What is still owed on the order: the total less what has been paid (never below zero).</summary>
    public decimal BalanceDue => Math.Max(0m, Totals.GrandTotal - AmountPaid);

    private OrderAggregate() { }

    public OrderAggregate(Guid id) : base(id) { }

    // ── placement ───────────────────────────────────────────────────────────────────────────────────
    /// <summary>Creates the order from a priced quote. A bank-transfer or counter order starts <c>PendingPayment</c>; cash on delivery and pay on collection
    /// are accepted at once (<c>Confirmed</c>, ADR-203). One history row records the start.</summary>
    public void Place(PlaceOrderDto dto)
    {
        ValidatePairing(dto);
        ValidateLines(dto);
        ValidateTotals(dto);

        OrderNumber = dto.OrderNumber;
        Channel = dto.Channel;
        CustomerId = dto.CustomerId;
        BranchId = dto.BranchId;
        FulfilmentMethod = dto.Fulfilment;
        PaymentMethod = dto.PaymentMethod;
        CurrencyCode = dto.CurrencyCode.ToUpperInvariant();
        Fulfilment = dto.Details;
        Totals = dto.Totals;
        DeliveryTaxAmount = dto.DeliveryTaxAmount;
        DiscountCodeId = dto.DiscountCodeId;
        DiscountCodeText = dto.DiscountCodeText;
        QuoteFingerprint = dto.QuoteFingerprint;
        IdempotencyKey = dto.IdempotencyKey;
        PlacedOn = dto.PlacedOn;

        _lines.AddRange(dto.Lines.Select(l => new OrderLine(
            l.LineId, l.LineNumber, l.SkuId, l.ProductId, l.ProductName, l.VariantLabel, l.Barcode, l.ReturnClass, l.IsBundle, l.CatId, l.Quantity, l.UnitPrice,
            l.LineSubtotal, l.LineDiscount, l.OrderDiscountShare, l.TaxClassId, l.TaxRatePercent, l.TaxAmount, l.LineTotal, l.Components)));

        Status = OrderTransitionRules.InitialStatus(dto.PaymentMethod);
        AppendHistory(null, Status, dto.PlacedOn, dto.UserContext.UserId, null);
        if (Status == OrderStatus.Confirmed) ConfirmedOn = dto.PlacedOn;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new OrderPlacedEvent
        {
            OrderId = Id, CustomerId = CustomerId, BranchId = BranchId, Channel = Channel.Value, PaymentMethod = PaymentMethod.Value,
            FulfilmentMethod = FulfilmentMethod.Value, Status = Status.Value, GrandTotal = new Money(Totals.GrandTotal, CurrencyCode),
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });

        if (Status == OrderStatus.Confirmed)
            AddDomainEvent(new OrderConfirmedEvent { OrderId = Id, InvoiceId = null, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });

        UpdateVersion();
    }

    /// <summary>A bank-transfer order is held <c>PendingPayment</c> until <paramref name="dueBy"/> (BR-16, ADR-203).</summary>
    public void AwaitTransfer(DateTimeOffset dueBy)
    {
        if (PaymentMethod != OrderPaymentMethod.BankTransfer || Status != OrderStatus.PendingPayment)
            throw new InvalidOperationException("Only a new bank-transfer order awaits a transfer.");

        PaymentDueBy = dueBy;
        UpdateVersion();
    }

    /// <summary>
    /// Records the batches Inventory reserved (D-4). For each line named in <paramref name="allocations"/> the quantities per SKU (the line's own, or each
    /// component of a bundle) must add up to exactly what the line needs -- else <see cref="InvalidOperationException"/>: Inventory's reservation port either
    /// reserves the whole demand or refuses. Calling it again for a line replaces that line's allocations (a staff reallocation, US-IMP-07 AC2).
    /// </summary>
    public void RecordAllocations(IReadOnlyList<AllocationDto> allocations)
    {
        if (Status != OrderStatus.PendingPayment && Status != OrderStatus.Confirmed && Status != OrderStatus.Packing)
            throw new InvalidOperationException($"Allocations cannot be recorded on a {Status.DisplayName} order.");

        foreach (var byLine in allocations.GroupBy(a => a.LineId))
        {
            var line = _lines.FirstOrDefault(l => l.Id == byLine.Key) ?? throw new InvalidOperationException($"Order line {byLine.Key} does not exist.");
            if (byLine.Any(a => a.Quantity <= 0)) throw new InvalidOperationException("An allocation quantity must be above zero.");
            if (byLine.Count() > OrderRules.MaxAllocationsPerLine * Math.Max(1, line.RequiredBySku().Count))
                throw new InvalidOperationException("A line cannot be spread over so many batches.");

            foreach (var (skuId, required) in line.RequiredBySku())
            {
                var allocated = byLine.Where(a => a.SkuId == skuId).Sum(a => a.Quantity);
                if (allocated != required)
                    throw new InvalidOperationException($"Line {line.LineNumber} needs {required} of SKU {skuId} allocated but {allocated} was given.");
            }

            if (byLine.Any(a => !line.RequiredBySku().ContainsKey(a.SkuId))) throw new InvalidOperationException("An allocation names a SKU the line does not need.");

            line.ReplaceAllocations(byLine.Select(a => new OrderLineAllocation(Guid.NewGuid(), a.SkuId, a.BatchId, a.LotNumber, a.LocationId, a.Quantity, a.BatchExpiresOn)));
        }

        UpdateVersion();
    }

    /// <summary>
    /// A staff override of the FEFO allocation of one line at picking (US-IMP-07 AC2, order half): while the order is <c>Confirmed</c> or <c>Packing</c> the
    /// line's batches are replaced by what Inventory re-reserved (the caller released the old reservation and reserved the explicit batch with a reason). The
    /// event names the batch it left and the batch it moved to -- ids only; the reason is in the audit trail Inventory enlists.
    /// </summary>
    public void ReallocateLine(Guid lineId, IReadOnlyList<AllocationDto> allocations, UserContext user, Guid correlationId)
    {
        if (Status != OrderStatus.Confirmed && Status != OrderStatus.Packing)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidOrderTransition, "Only an order that is confirmed or being packed can have its stock reallocated.", nameof(Status));

        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new DomainRuleViolationException(BusinessViolationCodes.OrderNotFound, "That order line was not found.", nameof(lineId));

        var from = line.Allocations.Select(a => a.BatchId).FirstOrDefault();
        RecordAllocations(allocations.Where(a => a.LineId == lineId).ToList());

        AddDomainEvent(new OrderLineReallocatedEvent
        {
            OrderId = Id, LineId = lineId, FromBatchId = from, ToBatchId = allocations.First(a => a.LineId == lineId).BatchId, CorrelationId = correlationId, UserContext = user
        });
        UpdateVersion();
    }

    /// <summary>Whether every line is allocated in full -- the precondition of dispatch and collection.</summary>
    public bool IsFullyAllocated =>
        _lines.All(l => l.RequiredBySku().All(r => l.Allocations.Where(a => a.SkuId == r.Key).Sum(a => a.Quantity) == r.Value));

    public void EnsureFullyAllocated()
    {
        if (!IsFullyAllocated)
            throw new DomainRuleViolationException(BusinessViolationCodes.OrderNotFullyAllocated, "This order has items that are not reserved from stock. Reallocate them first.", nameof(Lines));
    }

    // ── money in ────────────────────────────────────────────────────────────────────────────────────
    /// <summary>The customer's own transfer reference (FR-ORD-22); overwritten while the order is still pending.</summary>
    public void SubmitTransferReference(string reference, DateTimeOffset now, UserContext user, Guid correlationId)
    {
        EnsureAwaitingTransfer();
        if (string.IsNullOrWhiteSpace(reference) || reference.Trim().Length > OrderRules.MaxTransferReferenceLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.OrderNotAwaitingPayment, $"A transfer reference is required (up to {OrderRules.MaxTransferReferenceLength} characters).", nameof(reference));

        OrderFreeText.RefuseCardNumber(reference, nameof(reference)); // C-5
        CustomerTransferReference = reference.Trim();
        TransferReportedOn = now;
        AddDomainEvent(new OrderTransferReferenceSubmittedEvent { OrderId = Id, CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    /// <summary>The amount staff confirm must equal the grand total exactly (OQ-4) -- else <c>TransferAmountMismatch</c>.</summary>
    public void EnsureTransferAmount(decimal amount)
    {
        if (amount != Totals.GrandTotal)
            throw new DomainRuleViolationException(BusinessViolationCodes.TransferAmountMismatch,
                $"The amount must be exactly {CurrencyCode} {Totals.GrandTotal:0.00}. If the customer paid a different amount, contact them first.", nameof(amount));
    }

    /// <summary>Whether staff may confirm a transfer now: the order awaits a transfer (<c>OrderNotAwaitingPayment</c>), no confirmation is already waiting for a
    /// second person (<c>PaymentAlreadyAwaitingApproval</c>), and the amount is exactly the total (<c>TransferAmountMismatch</c>), and the typed bank
    /// reference is not a card number (C-5).</summary>
    public void EnsureCanConfirmTransfer(decimal amount, string? bankReference)
    {
        EnsureAwaitingTransfer();
        if (HeldPayment is not null)
            throw new DomainRuleViolationException(BusinessViolationCodes.PaymentAlreadyAwaitingApproval, "A confirmation for this order is already waiting for approval.", nameof(HeldPayment));

        EnsureTransferAmount(amount);
        OrderFreeText.RefuseCardNumber(bankReference, nameof(bankReference));
    }

    /// <summary>A confirmation above Billing's approval threshold is held for a second person (ADR-204). Nothing else changes.</summary>
    public void HoldPaymentForApproval(HeldPaymentConfirmation held, UserContext user, Guid correlationId)
    {
        EnsureAwaitingTransfer();
        if (HeldPayment is not null)
            throw new DomainRuleViolationException(BusinessViolationCodes.PaymentAlreadyAwaitingApproval, "A confirmation for this order is already waiting for approval.", nameof(HeldPayment));

        EnsureTransferAmount(held.Amount);
        OrderFreeText.RefuseCardNumber(held.Reference, nameof(held.Reference));
        HeldPayment = held;
        AddDomainEvent(new OrderPaymentHeldForApprovalEvent { OrderId = Id, Amount = new Money(held.Amount, CurrencyCode), CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    /// <summary>The held confirmation, for the approver to complete. <c>NoPaymentAwaitingApproval</c> if none; <c>SecondApproverMustDiffer</c> if the approver
    /// is the person who recorded it (BR-17, ADR-204).</summary>
    public HeldPaymentConfirmation RequireHeldPaymentForApprover(Guid approverId)
    {
        EnsureAwaitingTransfer();
        var held = HeldPayment ?? throw new DomainRuleViolationException(BusinessViolationCodes.NoPaymentAwaitingApproval, "No payment is waiting for approval on this order.", nameof(HeldPayment));

        if (held.RecordedBy == approverId)
            throw new DomainRuleViolationException(BusinessViolationCodes.SecondApproverMustDiffer, "A different staff member must approve this payment.", nameof(approverId));

        return held;
    }

    public void RejectHeldPayment(UserContext user, Guid correlationId)
    {
        EnsureAwaitingTransfer();
        if (HeldPayment is null)
            throw new DomainRuleViolationException(BusinessViolationCodes.NoPaymentAwaitingApproval, "No payment is waiting for approval on this order.", nameof(HeldPayment));

        HeldPayment = null;
        AddDomainEvent(new OrderHeldPaymentRejectedEvent { OrderId = Id, CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    /// <summary>Staff verified the transfer: records the Billing payment, clears any hold and moves <c>PendingPayment -> Confirmed</c>.</summary>
    public void ConfirmPayment(Guid? paymentId, string paymentMethodName, decimal amount, DateTimeOffset now, UserContext user, Guid correlationId)
    {
        EnsureAwaitingTransfer();
        EnsureTransferAmount(amount);

        if (paymentId is { } id) RecordPayment(id, paymentMethodName, amount, now);
        HeldPayment = null;
        Transition(OrderTransitionAction.ConfirmPayment, user.UserId, now, null);
        ConfirmedOn = now;

        AddDomainEvent(new OrderConfirmedEvent { OrderId = Id, InvoiceId = InvoiceId, CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    /// <summary>Adds a Billing payment reference and counts it into <see cref="AmountPaid"/>.</summary>
    public void RecordPayment(Guid paymentId, string method, decimal amount, DateTimeOffset now)
    {
        if (_payments.Count >= OrderRules.MaxPaymentRefs)
            throw new InvalidOperationException("An order cannot carry so many payments.");

        if (amount <= 0) throw new InvalidOperationException("A payment amount must be above zero.");

        _payments.Add(new OrderPaymentRef(Guid.NewGuid(), paymentId, method, amount, now));
        AmountPaid += amount;
        UpdateVersion();
    }

    /// <summary>Links the invoice Billing issued (the number is a display copy).</summary>
    public void AttachInvoice(Guid invoiceId, string invoiceNumber)
    {
        InvoiceId = invoiceId;
        InvoiceNumber = invoiceNumber;
        UpdateVersion();
    }

    public void AddCreditNote(Guid creditNoteId)
    {
        if (CreditNoteIds.Contains(creditNoteId)) return;
        if (CreditNoteIds.Count >= OrderRules.MaxCreditNotes) throw new InvalidOperationException("An order cannot carry so many credit notes.");

        CreditNoteIds = [.. CreditNoteIds, creditNoteId];
        UpdateVersion();
    }

    // ── expiry and cancellation ─────────────────────────────────────────────────────────────────────
    /// <summary>The payment window passed (BR-16): <c>PendingPayment -> Cancelled</c> with <c>PaymentWindowExpired</c>. Money claimed received is not expired:
    /// a held confirmation, or a due time not yet reached, is <c>OrderNotExpirable</c>.</summary>
    public void Expire(DateTimeOffset now, UserContext user, Guid correlationId)
    {
        if (PaymentMethod != OrderPaymentMethod.BankTransfer || Status != OrderStatus.PendingPayment || PaymentDueBy is not { } due || now < due || HeldPayment is not null)
            throw new DomainRuleViolationException(BusinessViolationCodes.OrderNotExpirable, "This order is not due to expire.", nameof(Status));

        Transition(OrderTransitionAction.Expire, user.UserId, now, "Payment window expired");
        CancellationReason = OrderCancellationReason.PaymentWindowExpired;
        CancelledOn = now;
        CancelledBy = user.UserId;
        CancelledByCustomer = false;

        AddDomainEvent(new OrderCancelledEvent { OrderId = Id, Reason = CancellationReason.Value, WasPaid = false, ByCustomer = false, CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    /// <summary>
    /// A customer may cancel while <c>PendingPayment</c> or <c>Confirmed</c>; staff may also cancel while <c>Packing</c> (US-ORD-12). After packing the
    /// customer is told to contact the shop (<c>CancellationNotAvailable</c>). A paid order stays <c>Cancelled</c> and raises a refund obligation
    /// (<see cref="RefundDue"/>, ADR-211).
    /// </summary>
    public void Cancel(UserContext user, bool byCustomer, OrderCancellationReason reason, string? note, DateTimeOffset now, Guid correlationId)
    {
        if (byCustomer && Status != OrderStatus.PendingPayment && Status != OrderStatus.Confirmed && Status != OrderStatus.Cancelled)
            throw new DomainRuleViolationException(BusinessViolationCodes.CancellationNotAvailable, OrderMessages.CancellationNotAvailable, nameof(Status));

        if (!byCustomer && reason == OrderCancellationReason.StaffDecision && string.IsNullOrWhiteSpace(note))
            throw new DomainRuleViolationException(BusinessViolationCodes.CancellationReasonRequired, "Give a reason for cancelling this order.", nameof(note));

        if (note is { Length: > OrderRules.MaxReasonLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.CancellationReasonRequired, $"A cancellation note is up to {OrderRules.MaxReasonLength} characters.", nameof(note));

        var wasPaid = AmountPaid > 0;
        Transition(OrderTransitionAction.Cancel, user.UserId, now, byCustomer ? "Cancelled by the customer" : "Cancelled by the shop");

        CancellationReason = byCustomer ? OrderCancellationReason.CustomerRequest : reason;
        CancellationNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        CancelledOn = now;
        CancelledBy = user.UserId;
        CancelledByCustomer = byCustomer;
        HeldPayment = null;
        if (wasPaid) RefundDue = AmountPaid - AmountRefunded;

        AddDomainEvent(new OrderCancelledEvent { OrderId = Id, Reason = CancellationReason.Value, WasPaid = wasPaid, ByCustomer = byCustomer, CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    /// <summary>Gives the discount use back when the order is cancelled or expires before dispatch (ADR-207). Returns true once, so the handler releases the
    /// tracked code exactly once.</summary>
    public bool ReleaseDiscount()
    {
        if (DiscountCodeId is null || DiscountReleased) return false;

        DiscountReleased = true;
        UpdateVersion();
        return true;
    }

    // ── fulfilment ──────────────────────────────────────────────────────────────────────────────────
    public void StartPacking(UserContext user, DateTimeOffset now, Guid correlationId)
    {
        Transition(OrderTransitionAction.StartPacking, user.UserId, now, null);
        PackingStartedOn = now;
        AddDomainEvent(new OrderPackingStartedEvent { OrderId = Id, CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    /// <summary>Dispatch a delivery order (<c>Packing -> Dispatched</c>). The caller has already checked the batches are neither recalled nor expired (BR-14).</summary>
    public void Dispatch(Shipment shipment, DateTimeOffset now, UserContext user, Guid correlationId)
    {
        if (FulfilmentMethod != FulfilmentMethod.Delivery)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidOrderTransition, "A pickup order is marked ready for collection, not dispatched.", nameof(FulfilmentMethod));

        EnsureFullyAllocated();
        Transition(OrderTransitionAction.Dispatch, user.UserId, now, null);
        Shipment = shipment;

        AddDomainEvent(new OrderDispatchedEvent { OrderId = Id, CourierId = shipment.CourierId, HasTracking = !string.IsNullOrWhiteSpace(shipment.TrackingNumber), CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    /// <summary>A pickup order is packed and waiting at the branch (<c>Packing -> Dispatched</c>, "ready for collection"); held until <paramref name="collectBy"/>.</summary>
    public void MarkReadyForCollection(DateOnly collectBy, DateTimeOffset now, UserContext user, Guid correlationId)
    {
        if (FulfilmentMethod != FulfilmentMethod.Pickup)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidOrderTransition, "Only a pickup order is marked ready for collection.", nameof(FulfilmentMethod));

        EnsureFullyAllocated();
        Transition(OrderTransitionAction.MarkReadyForCollection, user.UserId, now, null);
        ReadyForCollectionOn = now;
        CollectByDate = collectBy;

        AddDomainEvent(new OrderReadyForCollectionEvent { OrderId = Id, CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    /// <summary>Sets the tracking number (and link) of a dispatched order. Returns true when this is the <b>first</b> number -- the customer is told once.</summary>
    public bool RecordTracking(string trackingNumber, string? trackingUrl)
    {
        if (Status != OrderStatus.Dispatched || Shipment is null)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidOrderTransition, "Only a dispatched order can have a tracking number.", nameof(Status));

        if (string.IsNullOrWhiteSpace(trackingNumber) || trackingNumber.Trim().Length > OrderRules.MaxTrackingNumberLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidOrderTransition, $"A tracking number is required (up to {OrderRules.MaxTrackingNumberLength} characters).", nameof(trackingNumber));

        var first = string.IsNullOrWhiteSpace(Shipment.TrackingNumber);
        Shipment = Shipment with { TrackingNumber = trackingNumber.Trim(), TrackingUrl = trackingUrl };
        UpdateVersion();
        return first;
    }

    /// <summary>A courier webhook event: only a <b>newer</b> event updates the last status (returns whether it did). A "delivered" report does not deliver the
    /// order -- COD money must be recorded by a person (US-ORD-20 AC2).</summary>
    public bool ApplyTrackingEvent(string status, DateTimeOffset occurredOn)
    {
        if ((Status != OrderStatus.Dispatched && Status != OrderStatus.Delivered) || Shipment is null) return false;
        if (Shipment.LastTrackingEventOn is { } last && occurredOn <= last) return false;

        Shipment = Shipment with { LastTrackingStatus = status.Length > 100 ? status[..100] : status, LastTrackingEventOn = occurredOn };
        UpdateVersion();
        return true;
    }

    /// <summary>
    /// Hand-over (<c>Dispatched -> Delivered</c>; a pickup order's "collected"). Cash on delivery needs the collection (<c>CodCollectionRequired</c>) and it must
    /// equal the balance; pay on collection needs a payment equal to the balance (<c>CollectionPaymentMismatch</c>), and an in-store card needs the terminal
    /// slip reference (<c>TerminalReferenceRequired</c>, C-5). A bank-transfer order was paid before packing.
    /// </summary>
    public void RecordDelivery(DateTimeOffset deliveredOn, CodCollection? cod, CollectionPaymentDto? payment, DateTimeOffset now, UserContext user, Guid correlationId)
    {
        OrderTransitionRules.Require(Status, OrderTransitionAction.RecordDelivery); // anything but Dispatched -> InvalidOrderTransition, naming both states
        if (PaymentMethod == OrderPaymentMethod.CashOnDelivery)
        {
            if (cod is null) throw new DomainRuleViolationException(BusinessViolationCodes.CodCollectionRequired, "Record the cash collected on delivery: the amount, who collected it and when.", nameof(cod));
            if (cod.Amount != BalanceDue) throw new DomainRuleViolationException(BusinessViolationCodes.CollectionPaymentMismatch, $"The cash collected must be exactly {CurrencyCode} {BalanceDue:0.00}.", nameof(cod));
        }
        else if (PaymentMethod == OrderPaymentMethod.PayOnCollection)
        {
            if (payment is null) throw new DomainRuleViolationException(BusinessViolationCodes.CollectionPaymentMismatch, "Record the payment taken at collection.", nameof(payment));
            if (payment.Method == TenderMethod.InStoreCard && string.IsNullOrWhiteSpace(payment.Reference))
                throw new DomainRuleViolationException(BusinessViolationCodes.TerminalReferenceRequired, "Enter the card terminal slip reference.", nameof(payment));

            if (payment.Amount != BalanceDue) throw new DomainRuleViolationException(BusinessViolationCodes.CollectionPaymentMismatch, $"The payment must be exactly {CurrencyCode} {BalanceDue:0.00}.", nameof(payment));
        }

        Transition(OrderTransitionAction.RecordDelivery, user.UserId, now, null);
        DeliveredOn = deliveredOn;
        DeliveryRecordedBy = user.UserId;
        CodCollection = cod;

        AddDomainEvent(new OrderDeliveredEvent { OrderId = Id, HadCodCollection = cod is not null, CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    /// <summary>A parcel that did not reach the customer (<c>Dispatched -> Returned</c>, US-ORD-20 AC3). <c>Uncollected</c> is for pickup orders only;
    /// <c>Refused</c> and <c>Undeliverable</c> for delivery orders only.</summary>
    public void RecordDeliveryFailure(DeliveryFailureReason reason, DateTimeOffset now, UserContext user, Guid correlationId)
    {
        var isPickup = FulfilmentMethod == FulfilmentMethod.Pickup;
        if (isPickup != (reason == DeliveryFailureReason.Uncollected))
            throw new DomainRuleViolationException(BusinessViolationCodes.DeliveryFailureReasonInvalid,
                isPickup ? "A pickup order that was not collected is recorded as uncollected." : "A delivery that failed is recorded as refused or undeliverable.", nameof(reason));

        Transition(OrderTransitionAction.RecordDeliveryFailure, user.UserId, now, null);
        DeliveryFailureReason = reason;
        if (AmountPaid > 0m) RefundDue = Math.Max(0m, AmountPaid - AmountRefunded); // money already received (a transfer) is owed back

        AddDomainEvent(new OrderDeliveryFailedEvent { OrderId = Id, Reason = reason.Value, CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    // ── returns and refunds ─────────────────────────────────────────────────────────────────────────
    /// <summary>Counts units received back for a line. Every line fully returned while <c>Delivered</c> moves the order to <c>Returned</c>.</summary>
    public void RegisterReturnedQuantity(Guid lineId, int quantity, DateTimeOffset now, UserContext user, Guid correlationId)
    {
        if (Status != OrderStatus.Delivered && Status != OrderStatus.Returned)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReturnQuantityExceeded, "Only items of a delivered order can be returned.", nameof(Status));

        var line = _lines.FirstOrDefault(l => l.Id == lineId) ?? throw new DomainRuleViolationException(BusinessViolationCodes.ReturnQuantityExceeded, "That order line was not found.", nameof(lineId));
        if (quantity < 1 || quantity > line.Quantity - line.QuantityReturned)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReturnQuantityExceeded, "More items were returned than were delivered.", nameof(quantity));

        line.AddReturned(quantity);

        if (Status == OrderStatus.Delivered && _lines.All(l => l.QuantityReturned == l.Quantity))
        {
            Transition(OrderTransitionAction.CompleteReturn, user.UserId, now, null);
            AddDomainEvent(new OrderReturnedEvent { OrderId = Id, CorrelationId = correlationId, UserContext = user });
        }

        UpdateVersion();
    }

    /// <summary>A refund is recorded against the order (ADR-211). Never above what was paid; reduces <see cref="RefundDue"/> (floor 0); a full refund of a
    /// delivered or returned order moves it to <c>Refunded</c>. A cancelled paid order stays <c>Cancelled</c>.</summary>
    public void ApplyRefund(decimal amount, DateTimeOffset now, UserContext user, Guid correlationId)
    {
        if (amount <= 0 || AmountRefunded + amount > AmountPaid)
            throw new DomainRuleViolationException(BusinessViolationCodes.RefundExceedsAmountPaid, "A refund cannot be more than what was paid and not yet refunded.", nameof(amount));

        AmountRefunded += amount;
        RefundDue = Math.Max(0m, RefundDue - amount);

        if (AmountRefunded == AmountPaid && (Status == OrderStatus.Delivered || Status == OrderStatus.Returned))
        {
            Transition(OrderTransitionAction.Refund, user.UserId, now, null);
            AddDomainEvent(new OrderRefundedEvent { OrderId = Id, CorrelationId = correlationId, UserContext = user });
        }

        UpdateVersion();
    }

    // ── the counter ─────────────────────────────────────────────────────────────────────────────────
    /// <summary>Completes a counter sale in one call: <c>PendingPayment -> Confirmed -> Delivered</c> (three history rows with the placement's). The tenders
    /// recorded so far must cover the grand total (<c>TendersDoNotCoverTotal</c>).</summary>
    public void CompleteCounterSale(decimal changeGiven, DateTimeOffset now, UserContext user, Guid correlationId)
    {
        if (Channel != OrderChannel.Counter || Status != OrderStatus.PendingPayment)
            throw new InvalidOperationException("Only a new counter sale is completed this way.");

        if (AmountPaid != Totals.GrandTotal)
            throw new DomainRuleViolationException(BusinessViolationCodes.TendersDoNotCoverTotal, "The payments must add up to exactly the total of the sale.", nameof(AmountPaid));

        if (changeGiven < 0m) throw new InvalidOperationException("Change cannot be negative.");

        ChangeGiven = changeGiven;
        Transition(OrderTransitionAction.ConfirmPayment, user.UserId, now, null);
        ConfirmedOn = now;
        Transition(OrderTransitionAction.CollectAtCounter, user.UserId, now, null);
        DeliveredOn = now;
        DeliveryRecordedBy = user.UserId;

        AddDomainEvent(new OrderConfirmedEvent { OrderId = Id, InvoiceId = InvoiceId, CorrelationId = correlationId, UserContext = user });
        AddDomainEvent(new CounterSaleCompletedEvent { OrderId = Id, GrandTotal = new Money(Totals.GrandTotal, CurrencyCode), CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }

    // ── customer data (HO-ACC-001 ADR-039, ADR-214) ─────────────────────────────────────────────────
    /// <summary>The customer was merged into another: the order is reassigned. Idempotent.</summary>
    public bool ReassignCustomer(Guid survivingCustomerId)
    {
        if (CustomerId == survivingCustomerId) return false;

        CustomerId = survivingCustomerId;
        UpdateVersion();
        return true;
    }

    /// <summary>The customer was anonymised (D-11): a terminal order is redacted now; an open one is flagged and redacted at its terminal transition.
    /// Idempotent. Nothing is deleted (ADR-214).</summary>
    public bool FlagCustomerAnonymised()
    {
        if (CustomerAnonymised) return false;

        CustomerAnonymised = true;
        if (IsTerminal) Redact();
        UpdateVersion();
        return true;
    }

    public bool IsTerminal => Status == OrderStatus.Delivered || Status == OrderStatus.Cancelled || Status == OrderStatus.Returned || Status == OrderStatus.Refunded;

    /// <summary>The actions open to this order in its state (the same table the aggregate enforces).</summary>
    public IReadOnlyList<OrderTransitionAction> AllowedActions() => OrderTransitionRules.AllowedActions(Status, FulfilmentMethod, PaymentMethod);

    // ── internals ───────────────────────────────────────────────────────────────────────────────────
    private void Transition(OrderTransitionAction action, Guid by, DateTimeOffset now, string? note)
    {
        var next = OrderTransitionRules.Require(Status, action);
        AppendHistory(Status, next, now, by, note);
        Status = next;

        if (CustomerAnonymised && IsTerminal) Redact();
    }

    private void Redact() => Fulfilment = Fulfilment.Redacted();

    private void AppendHistory(OrderStatus? from, OrderStatus to, DateTimeOffset at, Guid by, string? note)
    {
        if (_statusHistory.Count >= OrderRules.MaxStatusHistory)
            throw new InvalidOperationException("An order cannot have so many status changes.");

        _statusHistory.Add(new OrderStatusChange(Guid.NewGuid(), from, to, at, by, note));
    }

    private void EnsureAwaitingTransfer()
    {
        if (PaymentMethod != OrderPaymentMethod.BankTransfer || Status != OrderStatus.PendingPayment)
            throw new DomainRuleViolationException(BusinessViolationCodes.OrderNotAwaitingPayment, "This order is not waiting for a bank transfer.", nameof(Status));
    }

    private static void ValidatePairing(PlaceOrderDto dto)
    {
        var valid =
            (dto.PaymentMethod == OrderPaymentMethod.CashOnDelivery && dto.Fulfilment == FulfilmentMethod.Delivery && dto.Channel == OrderChannel.Online) ||
            (dto.PaymentMethod == OrderPaymentMethod.PayOnCollection && dto.Fulfilment == FulfilmentMethod.Pickup && dto.Channel == OrderChannel.Online) ||
            (dto.PaymentMethod == OrderPaymentMethod.BankTransfer && dto.Fulfilment != FulfilmentMethod.Counter && dto.Channel == OrderChannel.Online) ||
            (dto.PaymentMethod == OrderPaymentMethod.InStore && dto.Fulfilment == FulfilmentMethod.Counter && dto.Channel == OrderChannel.Counter);

        if (!valid)
            throw new InvalidOperationException($"{dto.PaymentMethod.DisplayName} does not go with {dto.Fulfilment.DisplayName} on the {dto.Channel.DisplayName} channel.");

        if (dto.CustomerId is null && dto.Channel != OrderChannel.Counter)
            throw new InvalidOperationException("An online order belongs to a customer.");
    }

    private static void ValidateLines(PlaceOrderDto dto)
    {
        if (dto.Lines.Count == 0 || dto.Lines.Count > OrderRules.MaxLines)
            throw new InvalidOperationException($"An order has 1 to {OrderRules.MaxLines} lines.");

        foreach (var line in dto.Lines)
        {
            var unit = MinorUnits.FromAmount(line.UnitPrice);
            var subtotal = MinorUnits.FromAmount(line.LineSubtotal);
            if (line.Quantity < 1 || unit.Times(line.Quantity) != subtotal)
                throw new InvalidOperationException($"Line {line.LineNumber}: the subtotal is not quantity times unit price.");

            var expected = subtotal - MinorUnits.FromAmount(line.LineDiscount) - MinorUnits.FromAmount(line.OrderDiscountShare) + MinorUnits.FromAmount(line.TaxAmount);
            if (expected != MinorUnits.FromAmount(line.LineTotal))
                throw new InvalidOperationException($"Line {line.LineNumber}: the line total does not re-sum.");
        }
    }

    private static void ValidateTotals(PlaceOrderDto dto)
    {
        MinorUnits Sum(Func<OrderLineDto, decimal> pick) => new(dto.Lines.Sum(l => MinorUnits.FromAmount(pick(l)).Value));

        var t = dto.Totals;

        // FU1 ORD-55: the delivery tax is zero without a delivery charge, never negative and never more than the charge.
        var deliveryTax = MinorUnits.FromAmount(dto.DeliveryTaxAmount);
        if (deliveryTax.Value < 0 || deliveryTax.Value > MinorUnits.FromAmount(t.DeliveryCharge).Value)
            throw new InvalidOperationException("The delivery tax must be between zero and the delivery charge.");

        var ok =
            Sum(l => l.LineSubtotal) == MinorUnits.FromAmount(t.Subtotal) &&
            Sum(l => l.LineDiscount) == MinorUnits.FromAmount(t.LineDiscountTotal) &&
            Sum(l => l.OrderDiscountShare) == MinorUnits.FromAmount(t.OrderDiscount) &&
            Sum(l => l.TaxAmount) + MinorUnits.FromAmount(dto.DeliveryTaxAmount) == MinorUnits.FromAmount(t.TaxTotal) &&
            MinorUnits.FromAmount(t.Subtotal) - MinorUnits.FromAmount(t.LineDiscountTotal) - MinorUnits.FromAmount(t.OrderDiscount) + MinorUnits.FromAmount(t.DeliveryCharge) + MinorUnits.FromAmount(t.TaxTotal)
                == MinorUnits.FromAmount(t.GrandTotal);

        if (!ok) throw new InvalidOperationException("The order totals do not re-sum from its lines (FR-ORD-5).");
    }
}
