using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 §8.8 (US-BIL-11, 03, 04; ADR-182, 183, 185) -- <b>a record of money received</b>,
/// independent of how it arrived. Which fields a method needs is read from the method's own capability
/// flags (<c>RequiresReference</c>, <c>RequiresCollector</c>) -- this class never asks "which method is
/// this" (U-X4). Amount, method, reference, received date, collector and recorder have <b>no setter after
/// <see cref="Record"/></b> (US-BIL-11 AC3; <c>trg_payment_immutable</c> refuses it in the database too):
/// a mistake is reversed and re-entered, never edited. Where the money went is the append-only
/// <see cref="Entries"/>; <see cref="Unallocated"/> -- the part not applied to any invoice and not refunded
/// -- <b>is</b> the customer's credit (ADR-185).
/// </summary>
public sealed class PaymentAggregate : AggregateRoot
{
    private readonly List<AllocationEntry> _entries = [];
    private readonly List<RequestedAllocation> _requestedAllocations = [];

    public Guid BranchId { get; private set; }
    public Guid CustomerId { get; private set; }
    public PaymentMethod Method { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public DateOnly ReceivedOn { get; private set; }
    public string? ExternalReference { get; private set; }
    public PaymentCollector? Collector { get; private set; }
    public PaymentEarmark? Earmark { get; private set; }
    public PaymentStatus Status { get; private set; } = default!;
    public Guid RecordedBy { get; private set; }
    public DateTimeOffset RecordedOn { get; private set; }
    public string? Notes { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedOn { get; private set; }
    public Guid? RejectedBy { get; private set; }
    public DateTimeOffset? RejectedOn { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset? AcceptedOn { get; private set; }
    public DateOnly? BusinessDate { get; private set; }

    /// <summary>From <c>billing.receipt_number_seq</c> (gaps allowed -- a receipt number is not a legal
    /// document number); assigned by the database on insert.</summary>
    public long? ReceiptNumber { get; private set; }
    public Guid? ReversedBy { get; private set; }
    public DateTimeOffset? ReversedOn { get; private set; }
    public string? ReversalReason { get; private set; }
    public DateOnly? ReversalBusinessDate { get; private set; }
    public decimal RefundedTotal { get; private set; }
    public string? IdempotencyKey { get; private set; }

    /// <summary>FU1 D4 (ORD-52): set when the second approval was waived (<see cref="ApprovalWaiverReasons"/>); null for every ordinary payment.</summary>
    public string? ApprovalWaiverReason { get; private set; }

    public IReadOnlyList<AllocationEntry> Entries => _entries;
    public IReadOnlyList<RequestedAllocation> RequestedAllocations => _requestedAllocations;

    /// <summary>Σ Allocate − Σ Release across every invoice.</summary>
    public decimal NetAllocated => _entries.Sum(e => e.Kind == AllocationEntryKind.Allocate ? e.Amount : -e.Amount);

    /// <summary>Invariant ≥ 0. The customer's credit from this payment (ADR-185).</summary>
    public decimal Unallocated => Amount - NetAllocated - RefundedTotal;

    /// <summary>Money a customer may spend on any invoice: not earmarked, or earmark released.</summary>
    public bool IsFreeCredit => Status == PaymentStatus.Accepted && (Earmark is null || Earmark.IsReleased);

    public decimal AllocatedTo(Guid invoiceId) =>
        _entries.Where(e => e.InvoiceId == invoiceId).Sum(e => e.Kind == AllocationEntryKind.Allocate ? e.Amount : -e.Amount);

    private PaymentAggregate() { }

    public PaymentAggregate(Guid id) : base(id) { }

    // ── Record ──────────────────────────────────────────────────────────────────────────────────────
    public void Record(PaymentRecordDto dto)
    {
        if (dto.Amount <= 0m || decimal.Round(dto.Amount, 2) != dto.Amount || dto.Amount > BillingRules.MaxAmount)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "A payment amount must be greater than zero with at most two decimals.", nameof(dto.Amount));

        if (dto.ReceivedOn > dto.TodayLocal)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReceivedDateInFuture, "The date received cannot be in the future.", nameof(dto.ReceivedOn));

        if (dto.ReceivedOn < dto.TodayLocal.AddDays(-BillingRules.MaxBackdatedReceiptDays))
            throw new DomainRuleViolationException(BusinessViolationCodes.ReceivedDateTooOld, $"The date received cannot be more than {BillingRules.MaxBackdatedReceiptDays} days ago.", nameof(dto.ReceivedOn));

        var reference = string.IsNullOrWhiteSpace(dto.ExternalReference) ? null : dto.ExternalReference.Trim();
        if (reference is { Length: > BillingRules.MaxReferenceLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidSettingValue, $"A reference cannot exceed {BillingRules.MaxReferenceLength} characters.", nameof(dto.ExternalReference));

        // C-5: a card number is never recorded -- and never echoed back or logged.
        if (CardNumberGuard.LooksLikeCardNumber(reference) || CardNumberGuard.LooksLikeCardNumber(dto.Notes))
            throw new DomainRuleViolationException(BusinessViolationCodes.ReferenceLooksLikeCardNumber, BillingMessages.ReferenceLooksLikeCardNumber, nameof(dto.ExternalReference));

        if (dto.Method.RequiresReference && reference is null)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReferenceRequired, $"{dto.Method.ReferenceLabel} is required for this payment method.", nameof(dto.ExternalReference));

        if (dto.ApprovalWaiverReason is not null && !PaymentMethodRules.CanWaiveApproval(dto.Method, dto.ApprovalWaiverReason))
            throw new DomainRuleViolationException(BusinessViolationCodes.ApprovalWaiverNotAllowed, BillingMessages.ApprovalWaiverNotAllowed, nameof(dto.ApprovalWaiverReason));

        ValidateCollector(dto.Method, dto.Collector);
        ValidateEarmark(dto.Earmark);

        if (dto.Notes is { Length: > BillingRules.MaxNotesLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidSettingValue, $"Notes cannot exceed {BillingRules.MaxNotesLength} characters.", nameof(dto.Notes));

        if (dto.Allocations.Count > BillingRules.MaxRequestedAllocations || dto.Allocations.Any(a => a.Amount <= 0m))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "Each allocation must be greater than zero (at most 20).", nameof(dto.Allocations));

        if (dto.Allocations.Sum(a => a.Amount) > dto.Amount)
            throw new DomainRuleViolationException(BusinessViolationCodes.AllocationExceedsUnallocated,
                BillingMessages.AllocationExceedsUnallocated(dto.Amount, dto.CurrencyCode), nameof(dto.Allocations));

        BranchId = dto.BranchId;
        CustomerId = dto.CustomerId;
        Method = dto.Method;
        Amount = dto.Amount;
        CurrencyCode = dto.CurrencyCode;
        ReceivedOn = dto.ReceivedOn;
        ExternalReference = reference;
        Collector = dto.Collector;
        Earmark = dto.Earmark;
        Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        RecordedBy = dto.Actor.UserId;
        RecordedOn = dto.Now;
        IdempotencyKey = dto.IdempotencyKey;
        ReceiptNumber = dto.ReceiptNumber;
        ApprovalWaiverReason = dto.ApprovalWaiverReason;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new PaymentRecordedEvent
        {
            PaymentId = Id, CustomerId = CustomerId, BranchId = BranchId, Method = Method, Amount = Amount,
            CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext,
        });

        if (ApprovalWaiverReason is null && Amount > dto.ApprovalThreshold)
        {
            Status = PaymentStatus.PendingApproval;
            foreach (var requested in dto.Allocations)
                _requestedAllocations.Add(new RequestedAllocation(Guid.NewGuid(), requested.InvoiceId, requested.Amount));
        }
        else
        {
            if (ApprovalWaiverReason is not null)
                AddDomainEvent(new PaymentApprovalWaivedEvent { PaymentId = Id, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });

            MarkAccepted(dto.Now, dto.TodayLocal, dto.Actor);
        }

        UpdateVersion();
    }

    // ── Approval (ADR-183) ──────────────────────────────────────────────────────────────────────────
    /// <summary>Returns the allocations the recorder asked for; the handler applies each, capped at the
    /// invoice's outstanding at this moment, and reports what could not be applied.</summary>
    public IReadOnlyList<AllocationRequest> Approve(Guid approverId, DateTimeOffset now, DateOnly todayLocal, BillingActor actor)
    {
        RequirePending();
        if (approverId == RecordedBy)
            throw new DomainRuleViolationException(BusinessViolationCodes.ApproverMustDiffer, BillingMessages.ApproverMustDiffer, nameof(approverId));

        ApprovedBy = approverId;
        ApprovedOn = now;

        var requested = _requestedAllocations.Select(r => new AllocationRequest(r.InvoiceId, r.Amount)).ToList();
        _requestedAllocations.Clear();

        AddDomainEvent(new PaymentApprovedEvent { PaymentId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        MarkAccepted(now, todayLocal, actor);
        UpdateVersion();
        return requested;
    }

    public void Reject(Guid approverId, string reason, DateTimeOffset now, BillingActor actor)
    {
        RequirePending();
        if (approverId == RecordedBy)
            throw new DomainRuleViolationException(BusinessViolationCodes.ApproverMustDiffer, BillingMessages.ApproverMustDiffer, nameof(approverId));

        RequireReason(reason);

        Status = PaymentStatus.Rejected;
        RejectedBy = approverId;
        RejectedOn = now;
        RejectionReason = reason.Trim();
        _requestedAllocations.Clear();

        AddDomainEvent(new PaymentRejectedEvent { PaymentId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    // ── Allocation (append-only, ADR-185) ───────────────────────────────────────────────────────────
    public void Allocate(PaymentAllocateDto dto)
    {
        RequireAccepted();

        if (dto.Amount <= 0m)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "An allocation must be greater than zero.", nameof(dto.Amount));

        if (dto.Amount > Unallocated)
            throw new DomainRuleViolationException(BusinessViolationCodes.AllocationExceedsUnallocated,
                BillingMessages.AllocationExceedsUnallocated(Unallocated, CurrencyCode), nameof(dto.Amount));

        if (Earmark is { IsReleased: false } && !dto.InvoiceMatchesEarmark)
            throw new DomainRuleViolationException(BusinessViolationCodes.EarmarkMismatch,
                "This payment is set aside for a specific order or stay and can only be applied to its invoice, unless the earmark is released.", nameof(dto.InvoiceId));

        if (_entries.Count >= BillingRules.MaxAllocationEntries)
            throw new DomainRuleViolationException(BusinessViolationCodes.TooManyAllocations, "This payment has reached its limit of allocation entries.", nameof(dto.InvoiceId));

        _entries.Add(new AllocationEntry(Guid.NewGuid(), AllocationEntryKind.Allocate, dto.InvoiceId, dto.Amount, dto.Now, dto.Actor.UserId, null));
        AddDomainEvent(new PaymentAllocatedEvent { PaymentId = Id, InvoiceId = dto.InvoiceId, Amount = dto.Amount, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    /// <summary>Only <c>CreditNoteExcess</c> and <c>PaymentReversed</c> -- staff cannot "move" a payment; a
    /// mis-allocation is corrected by reversal and re-entry (US-BIL-11 AC3).</summary>
    public void Release(PaymentReleaseDto dto)
    {
        RequireAccepted();

        if (dto.Amount <= 0m || dto.Amount > AllocatedTo(dto.InvoiceId))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "More was released than was allocated to that invoice.", nameof(dto.Amount));

        _entries.Add(new AllocationEntry(Guid.NewGuid(), AllocationEntryKind.Release, dto.InvoiceId, dto.Amount, dto.Now, dto.Actor.UserId, dto.Reason));
        AddDomainEvent(new PaymentAllocationReleasedEvent { PaymentId = Id, InvoiceId = dto.InvoiceId, Amount = dto.Amount, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    public void ReleaseEarmark(string reason, DateTimeOffset now, BillingActor actor)
    {
        RequireAccepted();
        if (Earmark is null || Earmark.IsReleased)
            throw new DomainRuleViolationException(BusinessViolationCodes.PaymentNotEarmarked, "This payment has no earmark to release.", nameof(Earmark));

        RequireReason(reason);

        Earmark = Earmark with { ReleasedOn = now, ReleasedBy = actor.UserId, ReleaseReason = reason.Trim() };
        AddDomainEvent(new PaymentEarmarkReleasedEvent { PaymentId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    /// <summary>A refund draws down this payment's credit (the <c>RefundAggregate</c> raises the event).</summary>
    public void RecordRefund(decimal amount)
    {
        RequireAccepted();

        if (amount <= 0m)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "A refund must be greater than zero.", nameof(amount));

        if (amount > Unallocated)
            throw new DomainRuleViolationException(BusinessViolationCodes.RefundExceedsAvailable,
                BillingMessages.RefundExceedsAvailable(Unallocated, CurrencyCode), nameof(amount));

        RefundedTotal += amount;
        UpdateVersion();
    }

    // ── Reversal (US-BIL-11 AC3) ────────────────────────────────────────────────────────────────────
    /// <summary>Releases every effective allocation and returns them so the handler applies
    /// <c>ReleasePayment</c> on each invoice in the same save.</summary>
    public IReadOnlyList<AllocationRequest> Reverse(string reason, DateTimeOffset now, DateOnly todayLocal, BillingActor actor)
    {
        RequireAccepted();
        RequireReason(reason);

        if (RefundedTotal != 0m)
            throw new DomainRuleViolationException(BusinessViolationCodes.PaymentHasRefunds,
                "This payment has refunds against it and cannot be reversed. Record a new payment instead.", nameof(RefundedTotal));

        var released = new List<AllocationRequest>();
        foreach (var invoiceId in _entries.Select(e => e.InvoiceId).Distinct().ToList())
        {
            var outstanding = AllocatedTo(invoiceId);
            if (outstanding <= 0m) continue;

            _entries.Add(new AllocationEntry(Guid.NewGuid(), AllocationEntryKind.Release, invoiceId, outstanding, now, actor.UserId, AllocationReleaseReason.PaymentReversed));
            released.Add(new AllocationRequest(invoiceId, outstanding));
        }

        Status = PaymentStatus.Reversed;
        ReversedBy = actor.UserId;
        ReversedOn = now;
        ReversalReason = reason.Trim();
        ReversalBusinessDate = todayLocal;

        AddDomainEvent(new PaymentReversedEvent { PaymentId = Id, CustomerId = CustomerId, BranchId = BranchId, Amount = Amount, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
        return released;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────────
    private void MarkAccepted(DateTimeOffset now, DateOnly todayLocal, BillingActor actor)
    {
        Status = PaymentStatus.Accepted;
        AcceptedOn = now;
        BusinessDate = todayLocal;

        AddDomainEvent(new PaymentAcceptedEvent
        {
            PaymentId = Id, CustomerId = CustomerId, BranchId = BranchId, Method = Method, Amount = Amount,
            EarmarkKind = Earmark?.Kind, EarmarkId = Earmark?.SourceId,
            CorrelationId = actor.CorrelationId, UserContext = actor.UserContext,
        });
    }

    private void RequirePending()
    {
        if (Status != PaymentStatus.PendingApproval)
            throw new DomainRuleViolationException(BusinessViolationCodes.PaymentNotPending, "This payment is not waiting for approval.", nameof(Status));
    }

    private void RequireAccepted()
    {
        if (Status != PaymentStatus.Accepted)
            throw new DomainRuleViolationException(BusinessViolationCodes.PaymentNotAccepted, "This payment is not an accepted payment.", nameof(Status));
    }

    private static void RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > BillingRules.MaxReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReasonRequired, "A reason is required.", nameof(reason));
        BillingFreeText.RefuseCardNumber(reason, nameof(reason));
    }

    private static void ValidateCollector(PaymentMethod method, PaymentCollector? collector)
    {
        if (!method.RequiresCollector) return;

        var valid = collector is not null && (collector.Kind == CollectorKind.Staff
            ? collector.StaffAccountId is not null
            : !string.IsNullOrWhiteSpace(collector.CourierCode) && !string.IsNullOrWhiteSpace(collector.CourierName));

        if (!valid)
            throw new DomainRuleViolationException(BusinessViolationCodes.CollectorRequired, "Say who collected the money: a staff member, or a courier with its code and name.", nameof(collector));

        if (collector!.CourierCode is { Length: > BillingRules.MaxCourierCodeLength } || collector.CourierName is { Length: > BillingRules.MaxCourierNameLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.CollectorRequired, "The courier code or name is too long.", nameof(collector));
        BillingFreeText.RefuseCardNumber(collector.CourierCode, nameof(collector.CourierCode));
        BillingFreeText.RefuseCardNumber(collector.CourierName, nameof(collector.CourierName));
    }

    private static void ValidateEarmark(PaymentEarmark? earmark)
    {
        if (earmark is null) return;

        if (string.IsNullOrWhiteSpace(earmark.Label) || earmark.Label.Length > BillingRules.MaxEarmarkLabelLength || earmark.SourceId == Guid.Empty)
            throw new DomainRuleViolationException(BusinessViolationCodes.EarmarkMismatch, "An earmark needs a source and a label of up to 200 characters.", nameof(earmark));
        BillingFreeText.RefuseCardNumber(earmark.Label, nameof(earmark.Label));
    }
}
