using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Prescription;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Prescription;

/// <summary>
/// HO-PHM-001 §8.7 (ADR-143, ADR-147, ADR-154) -- one supply of a prescription: the exact batches handed over, the label frozen at that moment and the
/// price in force then. Its own root because dispensings accumulate without bound (bounded-contexts rule 3). Created and saved by the dispense
/// command in ONE <c>SaveChangesAsync</c> together with the prescription's supply count, the Inventory ledger entries and any register entries -- a
/// recorded rule-1 exception, atomic by F23. Reversal is the only later change; a reprint is counted and audited, and prints the same snapshot.
/// </summary>
public sealed class DispensingAggregate : AggregateRoot
{
    private readonly List<DispensingLine> _lines = [];

    public Guid PrescriptionId { get; private set; }
    public string PrescriptionNumber { get; private set; } = default!;
    public Guid BranchId { get; private set; }
    public Guid CatId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VisitId { get; private set; }
    public int SupplyNumber { get; private set; }
    public bool IsRepeat { get; private set; }
    public Guid? RepeatRequestId { get; private set; }
    public Guid DispensedBy { get; private set; }
    public DateTimeOffset DispensedOn { get; private set; }
    public DateOnly DispensedOnLocal { get; private set; }
    public Guid? WitnessId { get; private set; }
    public string? FefoOverrideReason { get; private set; }
    public MedicineLabelSnapshot Label { get; private set; } = default!;
    public int LabelPrintCount { get; private set; }
    public DispensingStatus Status { get; private set; } = DispensingStatus.Dispensed;
    public DateTimeOffset? ReversedOn { get; private set; }
    public Guid? ReversedBy { get; private set; }
    public string? ReversalReason { get; private set; }

    public IReadOnlyList<DispensingLine> Lines => _lines;

    private DispensingAggregate() { }

    public DispensingAggregate(Guid id) : base(id) { }

    public void Create(DispensingCreateDto dto)
    {
        if (dto.Lines.Count is < 1 or > DispensingRules.MaxLinesPerDispensing)
            throw Violation(BusinessViolationCodes.TooManyDispensingLines, $"A supply is dispensed in one to {DispensingRules.MaxLinesPerDispensing} batches.", nameof(dto.Lines));

        if (dto.Lines.Any(l => l.Quantity <= 0m) || dto.Lines.Select(l => l.BatchId).Distinct().Count() != dto.Lines.Count)
            throw Violation(BusinessViolationCodes.DispenseQuantityMismatch, "Each batch appears once with a quantity above zero.", nameof(dto.Lines));

        if (dto.SupplyNumber < 1)
            throw Violation(BusinessViolationCodes.NoSuppliesRemaining, "A supply number starts at one.", nameof(dto.SupplyNumber));

        var reason = dto.FefoOverrideReason?.Trim();
        if (reason is { Length: > DispensingRules.MaxReasonLength })
            throw Violation(BusinessViolationCodes.FefoOverrideReasonRequired, "The reason is at most 500 characters.", nameof(dto.FefoOverrideReason));

        PrescriptionId = dto.PrescriptionId;
        PrescriptionNumber = dto.PrescriptionNumber;
        BranchId = dto.BranchId;
        CatId = dto.CatId;
        CustomerId = dto.CustomerId;
        VisitId = dto.VisitId;
        SupplyNumber = dto.SupplyNumber;
        IsRepeat = dto.IsRepeat;
        RepeatRequestId = dto.RepeatRequestId;
        DispensedBy = dto.DispensedBy;
        DispensedOn = dto.DispensedOn;
        DispensedOnLocal = dto.DispensedOnLocal;
        WitnessId = dto.WitnessId;
        FefoOverrideReason = string.IsNullOrWhiteSpace(reason) ? null : reason;
        Label = dto.Label;
        LabelPrintCount = 0;
        Status = DispensingStatus.Dispensed;

        _lines.Clear();
        _lines.AddRange(dto.Lines.Select(l => new DispensingLine(l.LineId, l.SkuId, l.BatchId, l.LotNumber, l.ExpiresOn, l.Quantity, l.RegisterEntryId, l.Price)));

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new PrescriptionDispensedEvent
        {
            DispensingId = Id, PrescriptionId = PrescriptionId, BranchId = BranchId, CustomerId = CustomerId, CatId = CatId, VisitId = VisitId,
            SupplyNumber = SupplyNumber,
            Lines = _lines.Select(l => new DispensedLineFact(l.SkuId, l.BatchId, l.Quantity, l.PriceSnapshot?.UnitPrice, l.PriceSnapshot?.CurrencyCode)).ToList(),
            CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext
        });
        UpdateVersion();
    }

    /// <summary>A reprint is counted (the first print counts as one) and raises an event the audit handler records -- reprints of a controlled-drug
    /// label are worth knowing about.</summary>
    public void RecordLabelPrint(bool isControlled, PharmacyActor actor)
    {
        LabelPrintCount++;
        AddDomainEvent(new LabelPrintedEvent
        {
            DispensingId = Id, PrescriptionId = PrescriptionId, PrintCount = LabelPrintCount, IsControlled = isControlled,
            CorrelationId = actor.CorrelationId, UserContext = actor.UserContext
        });
        UpdateVersion();
    }

    /// <summary>Whether the window for reversing this supply is still open (<paramref name="windowHours"/> of zero means reversal is off).</summary>
    public bool IsWithinReversalWindow(DateTimeOffset now, int windowHours) => windowHours > 0 && now <= DispensedOn.AddHours(windowHours);

    public void Reverse(Guid by, string reason, DateTimeOffset now, int windowHours, PharmacyActor actor)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > DispensingRules.MaxReasonLength)
            throw Violation(BusinessViolationCodes.ReversalReasonRequired, "Give a reason of at most 500 characters.", nameof(reason));

        if (Status != DispensingStatus.Dispensed)
            throw Violation(BusinessViolationCodes.DispensingNotReversible, "This supply has already been reversed.", nameof(Status));

        if (!IsWithinReversalWindow(now, windowHours))
            throw Violation(BusinessViolationCodes.ReversalWindowPassed, "This supply was dispensed too long ago to reverse. Waste or return the medicine through Inventory instead.", nameof(DispensedOn));

        Status = DispensingStatus.Reversed;
        ReversedOn = now;
        ReversedBy = by;
        ReversalReason = reason.Trim();

        AddDomainEvent(new DispensingReversedEvent
        {
            DispensingId = Id, PrescriptionId = PrescriptionId, BranchId = BranchId, CustomerId = CustomerId, CatId = CatId, SupplyNumber = SupplyNumber,
            CorrelationId = actor.CorrelationId, UserContext = actor.UserContext
        });
        UpdateVersion();
    }

    /// <summary>A customer merge moves the dispensing to the survivor. The frozen label (the owner's name at the time) is never rewritten.</summary>
    public void ReassignCustomer(Guid survivorId)
    {
        if (CustomerId == survivorId) return;
        CustomerId = survivorId;
        UpdateVersion();
    }

    private static DomainRuleViolationException Violation(string code, string message, string property) => new(code, message, property);
}
