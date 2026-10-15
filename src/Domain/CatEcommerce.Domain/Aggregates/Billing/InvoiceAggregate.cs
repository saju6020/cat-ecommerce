using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Billing;

/// <summary>
/// HO-BIL-001 §8.6 (US-BIL-01, 02, 05, 07) -- a draft, then an issued, sequentially numbered, itemised,
/// tax-correct invoice. <b>After issue nothing about its content changes</b>: this aggregate exposes no
/// method that edits a line or a content field of a non-draft invoice (U-C5), and the database refuses it
/// too (ADR-180). Only the cached <see cref="PaidTotal"/>/<see cref="CreditedTotal"/> and the
/// <b>derived</b> <see cref="Status"/> move, through the three posting methods.
/// </summary>
public sealed class InvoiceAggregate : AggregateRoot
{
    private readonly List<InvoiceLine> _lines = [];

    public Guid BranchId { get; private set; }
    public Guid CustomerId { get; private set; }
    public InvoiceStatus Status { get; private set; } = default!;
    public string CurrencyCode { get; private set; } = default!;
    public string? Number { get; private set; }
    public string? FinancialYear { get; private set; }
    public long? SequenceNumber { get; private set; }
    public DateTimeOffset? IssuedOn { get; private set; }
    public DateOnly? IssueDate { get; private set; }
    public DateOnly? DueOn { get; private set; }
    public SellerSnapshot? Seller { get; private set; }
    public BillToSnapshot? BillTo { get; private set; }
    public IReadOnlyList<InvoiceLine> Lines => _lines;
    public decimal NetTotal { get; private set; }
    public decimal DiscountTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal Total { get; private set; }
    public decimal PaidTotal { get; private set; }
    public decimal CreditedTotal { get; private set; }
    public LoyaltyRedemption? LoyaltyRedemption { get; private set; }
    public Guid? CorrectsInvoiceId { get; private set; }
    public Guid? CorrectsCreditNoteId { get; private set; }
    public string? Notes { get; private set; }
    public Guid DraftedBy { get; private set; }
    public Guid? IssuedBy { get; private set; }
    public string? IssueIdempotencyKey { get; private set; }

    public decimal Outstanding => InvoiceStatusRules.Outstanding(Total, PaidTotal, CreditedTotal);

    private InvoiceAggregate() { }

    public InvoiceAggregate(Guid id) : base(id) { }

    // ── Draft ───────────────────────────────────────────────────────────────────────────────────────
    public void CreateDraft(DraftInvoiceDto dto)
    {
        RequireLineCount(dto.Lines.Count);

        BranchId = dto.BranchId;
        CustomerId = dto.CustomerId;
        CurrencyCode = dto.CurrencyCode;
        Status = InvoiceStatus.Draft;
        Notes = ValidatedNotes(dto.Notes);
        CorrectsInvoiceId = dto.CorrectsInvoiceId;
        CorrectsCreditNoteId = dto.CorrectsCreditNoteId;
        DraftedBy = dto.Actor.UserId;

        var order = 0;
        foreach (var input in dto.Lines)
            _lines.Add(new InvoiceLine(Guid.NewGuid(), input, order++));

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new DraftInvoiceCreatedEvent { InvoiceId = Id, CustomerId = CustomerId, BranchId = BranchId, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    /// <summary>Replaces the draft's charge lines (C6): lines whose charge stays are kept untouched, the rest
    /// removed or added. Returns the loyalty redemption this change cleared (its value no longer fits the
    /// qualifying net), if any -- the handler then posts a <c>RedemptionReversal</c>.</summary>
    public LoyaltyRedemption? SetDraftLines(IReadOnlyList<DraftInvoiceLineInput> lines, BillingActor actor)
    {
        RequireDraft();
        RequireLineCount(lines.Count);

        var wanted = lines.Where(l => l.ChargeId is not null).Select(l => l.ChargeId!.Value).ToHashSet();
        foreach (var line in _lines.Where(l => l.ChargeId is null || !wanted.Contains(l.ChargeId.Value)).ToList())
            _lines.Remove(line);

        var order = 0;
        foreach (var input in lines)
        {
            var existing = _lines.FirstOrDefault(l => l.ChargeId is not null && l.ChargeId == input.ChargeId);
            if (existing is null) _lines.Add(new InvoiceLine(Guid.NewGuid(), input, order));
            else existing.SetSortOrder(order);
            order++;
        }

        var cleared = ClearLoyaltyIfNoLongerFits();

        AddDomainEvent(new DraftInvoiceChangedEvent { InvoiceId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
        return cleared;
    }

    /// <summary>C18 -- a corrected draft's lines are editable (description, quantity, price, discount, tax
    /// class), but no charge lines may be added or removed; the lines are matched by position.</summary>
    public void ReviseCorrectedLines(IReadOnlyList<DraftInvoiceLineInput> lines, BillingActor actor)
    {
        RequireDraft();
        if (CorrectsInvoiceId is null)
            throw new DomainRuleViolationException(BusinessViolationCodes.CorrectedDraftInvalid, "Only a corrected draft's lines can be edited directly.", nameof(CorrectsInvoiceId));

        if (lines.Count != _lines.Count)
            throw new DomainRuleViolationException(BusinessViolationCodes.CorrectedDraftInvalid, "A corrected draft keeps the same lines; only their details change.", nameof(lines));
        foreach (var line in lines)
            BillingFreeText.RefuseCardNumber(line.Description, nameof(line.Description));


        var ordered = _lines.OrderBy(l => l.SortOrder).ToList();
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].ReviseDraft(lines[i], i);

        AddDomainEvent(new DraftInvoiceChangedEvent { InvoiceId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    /// <summary>Returns the redemption that was on the draft so the handler posts its reversal.</summary>
    public LoyaltyRedemption? Discard(BillingActor actor)
    {
        RequireDraft();

        foreach (var line in _lines) line.MarkInvoiceDiscarded();

        var redemption = LoyaltyRedemption;
        LoyaltyRedemption = null;
        Status = InvoiceStatus.Discarded;

        AddDomainEvent(new DraftInvoiceDiscardedEvent { InvoiceId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
        return redemption;
    }

    public void ApplyLoyaltyRedemption(LoyaltyRedemptionDto dto, BillingActor actor)
    {
        RequireDraft();
        if (LoyaltyRedemption is not null)
            throw new DomainRuleViolationException(BusinessViolationCodes.LoyaltyRedemptionExceedsNet, "This draft already has a redemption. Remove it first.", nameof(LoyaltyRedemption));

        var qualifying = QualifyingNetBeforeShare();
        if (dto.Value <= 0m || dto.Value > qualifying)
            throw new DomainRuleViolationException(BusinessViolationCodes.LoyaltyRedemptionExceedsNet, "The redemption cannot exceed the net of the lines that earn points.", nameof(dto.Points));

        LoyaltyRedemption = new LoyaltyRedemption(dto.Points, dto.Value, dto.LoyaltyEntryId);
        AddDomainEvent(new DraftInvoiceChangedEvent { InvoiceId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    public LoyaltyRedemption? RemoveLoyaltyRedemption(BillingActor actor)
    {
        RequireDraft();
        var removed = LoyaltyRedemption;
        if (removed is null) return null;

        LoyaltyRedemption = null;
        AddDomainEvent(new DraftInvoiceChangedEvent { InvoiceId = Id, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
        return removed;
    }

    /// <summary>HO-ACC-001 <c>CustomerMergedEvent</c> (ADR-192): only a draft moves; an issued invoice keeps its customer.</summary>
    public void ReassignOwner(Guid newCustomerId)
    {
        if (Status != InvoiceStatus.Draft || CustomerId == newCustomerId) return;

        CustomerId = newCustomerId;
        UpdateVersion();
    }

    // ── Issue ───────────────────────────────────────────────────────────────────────────────────────
    public void Issue(InvoiceIssueDto dto)
    {
        RequireDraft();
        if (_lines.Count == 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvoiceHasNoLines, "An invoice needs at least one line.", nameof(Lines));

        if (dto.Seller is null)
            throw new DomainRuleViolationException(BusinessViolationCodes.BillingProfileMissing, BillingMessages.BillingProfileMissing, nameof(Seller));

        var inputs = new List<CalcLineInput>();
        foreach (var line in _lines)
        {
            if (!dto.TaxRates.TryGetValue(line.TaxClassId, out var rate))
                throw new DomainRuleViolationException(BusinessViolationCodes.TaxClassNotFound, "A line's tax class no longer exists.", nameof(Lines));

            if (rate.RatePercent is null)
                throw new DomainRuleViolationException(BusinessViolationCodes.TaxRateNotConfigured, BillingMessages.TaxRateNotConfigured(rate.Code), nameof(Lines));

            inputs.Add(new CalcLineInput(line.Id, CurrencyCode, line.Quantity, line.UnitPrice, line.LineDiscount, rate.RatePercent.Value, line.ItemKind != ItemKind.Fee));
        }

        var totals = InvoiceCalculator.Calculate(inputs, LoyaltyRedemption?.Value ?? 0m);
        foreach (var result in totals.Lines)
        {
            var line = _lines.First(l => l.Id == result.Key);
            var rate = dto.TaxRates[line.TaxClassId];
            line.Freeze(rate.Code, rate.RatePercent!.Value, result.InvoiceDiscountShare, result.NetAmount, result.TaxAmount, result.GrossAmount);
        }

        NetTotal = totals.NetTotal;
        DiscountTotal = totals.DiscountTotal;
        TaxTotal = totals.TaxTotal;
        Total = totals.Total;

        Number = dto.Number;
        FinancialYear = dto.FinancialYear;
        SequenceNumber = dto.SequenceNumber;
        IssuedOn = dto.Now;
        IssueDate = dto.IssueDate;
        DueOn = dto.IssueDate.AddDays(dto.PaymentTermsDays);
        Seller = dto.Seller;
        BillTo = dto.BillTo;
        IssuedBy = dto.Actor.UserId;
        IssueIdempotencyKey = dto.IdempotencyKey;

        Status = InvoiceStatusRules.Derive(Total, PaidTotal, CreditedTotal);

        AddDomainEvent(new InvoiceIssuedEvent { InvoiceId = Id, CustomerId = CustomerId, BranchId = BranchId, Total = Total, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        if (Status != InvoiceStatus.Issued)
            AddDomainEvent(new InvoiceStatusChangedEvent { InvoiceId = Id, From = InvoiceStatus.Issued, To = Status, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });

        UpdateVersion();
    }

    /// <summary>Only after a numbering collision on the series row (ADR-179): the invoice has been issued in
    /// memory but not yet persisted, so the number it took is replaced by the freshly taken one. Never valid
    /// once anything has been posted against the invoice.</summary>
    public void Renumber(string number, long sequenceNumber)
    {
        if (Status == InvoiceStatus.Draft || Status == InvoiceStatus.Discarded || PaidTotal != 0m || CreditedTotal != 0m)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvoiceNotDraft, BillingMessages.InvoiceNotDraft, nameof(Number));

        Number = number;
        SequenceNumber = sequenceNumber;
    }

    // ── Posting (the only things that move after issue) ─────────────────────────────────────────────
    public void ApplyPayment(decimal amount, BillingActor actor)
    {
        if (!InvoiceStatusRules.IsIssuedState(Status))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvoiceNotIssued, "Payments can only be applied to an issued invoice.", nameof(Status));

        if (amount <= 0m)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "An amount must be greater than zero.", nameof(amount));

        if (amount > Outstanding)
            throw new DomainRuleViolationException(BusinessViolationCodes.AllocationExceedsOutstanding,
                BillingMessages.AllocationExceedsOutstanding(Outstanding, CurrencyCode, Number ?? string.Empty), nameof(amount));

        PaidTotal += amount;
        Rederive(actor);
    }

    public void ReleasePayment(decimal amount, BillingActor actor)
    {
        if (!InvoiceActionRules.CanReleasePayment(Status))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvoiceNotIssued, "Payments can only be released from an issued invoice.", nameof(Status));

        if (amount <= 0m || amount > PaidTotal)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidAmount, "More was released than was applied to this invoice.", nameof(amount));

        PaidTotal -= amount;
        Rederive(actor);
    }

    /// <summary>The handler releases any resulting overpayment first (§8.9), so the invariant
    /// <c>PaidTotal + CreditedTotal ≤ Total</c> holds after the credit.</summary>
    public void ApplyCredit(decimal amount, BillingActor actor)
    {
        if (!InvoiceActionRules.CanApplyCredit(Status))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvoiceNotIssued, "A credit can only be applied to an issued invoice.", nameof(Status));

        if (amount <= 0m || CreditedTotal + amount > Total)
            throw new DomainRuleViolationException(BusinessViolationCodes.CreditExceedsInvoice, "The credit is more than the invoice total.", nameof(amount));

        if (PaidTotal > Total - (CreditedTotal + amount))
            throw new DomainRuleViolationException(BusinessViolationCodes.CreditExceedsInvoice, "Release the payments covering the credited amount before applying the credit.", nameof(amount));

        CreditedTotal += amount;
        Rederive(actor);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────────
    private void Rederive(BillingActor actor)
    {
        var from = Status;
        Status = InvoiceStatusRules.Derive(Total, PaidTotal, CreditedTotal);

        if (Status != from)
            AddDomainEvent(new InvoiceStatusChangedEvent { InvoiceId = Id, From = from, To = Status, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });

        UpdateVersion();
    }

    private decimal QualifyingNetBeforeShare() =>
        _lines.Where(l => l.ItemKind != ItemKind.Fee).Sum(l => InvoiceCalculator.Round(l.Quantity * l.UnitPrice) - l.LineDiscount);

    private LoyaltyRedemption? ClearLoyaltyIfNoLongerFits()
    {
        if (LoyaltyRedemption is null || LoyaltyRedemption.Value <= QualifyingNetBeforeShare()) return null;

        var cleared = LoyaltyRedemption;
        LoyaltyRedemption = null;
        return cleared;
    }

    private void RequireDraft()
    {
        if (!InvoiceActionRules.CanChangeContent(Status))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvoiceNotDraft, BillingMessages.InvoiceNotDraft, nameof(Status));
    }

    private static void RequireLineCount(int count)
    {
        if (count == 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvoiceHasNoLines, "An invoice needs at least one line.", "Lines");

        if (count > BillingRules.MaxInvoiceLines)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvoiceTooManyLines, $"An invoice can have at most {BillingRules.MaxInvoiceLines} lines.", "Lines");
    }

    private static string? ValidatedNotes(string? notes)
    {
        if (notes is { Length: > BillingRules.MaxNotesLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidSettingValue, $"Notes cannot exceed {BillingRules.MaxNotesLength} characters.", nameof(Notes));

        BillingFreeText.RefuseCardNumber(notes, nameof(Notes));
        return string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}
