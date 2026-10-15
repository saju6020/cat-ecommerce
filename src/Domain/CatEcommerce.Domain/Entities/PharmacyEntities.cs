using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

// HO-PHM-001 §8 -- the owned children of the Prescription context's roots. Each has an application-minted Guid id (ADR-021: ValueGeneratedNever).

/// <summary>HO-PHM-001 §8.2 -- one interaction a formulary medicine lists: another medicine by id, or an ingredient/class by normalised term. Exactly one target.</summary>
public sealed class FormularyInteraction
{
    public Guid Id { get; private set; }
    public Guid? InteractsWithMedicineId { get; private set; }
    public string? InteractsWithTerm { get; private set; }
    public InteractionSeverity Severity { get; private set; } = default!;
    public string Note { get; private set; } = default!;

    private FormularyInteraction() { }

    public FormularyInteraction(Guid id, Guid? interactsWithMedicineId, string? interactsWithTerm, InteractionSeverity severity, string note)
    {
        Id = id;
        InteractsWithMedicineId = interactsWithMedicineId;
        InteractsWithTerm = interactsWithTerm;
        Severity = severity;
        Note = note;
    }
}

/// <summary>HO-PHM-001 §8.2 -- a catalogue SKU that IS this medicine. A SKU is linked to at most one medicine (unique index on <c>sku_id</c>).</summary>
public sealed class LinkedSku
{
    public Guid Id { get; private set; }
    public Guid SkuId { get; private set; }

    private LinkedSku() { }

    public LinkedSku(Guid id, Guid skuId)
    {
        Id = id;
        SkuId = skuId;
    }
}

/// <summary>HO-PHM-001 §8.3 / §8.5 -- exactly what was on the vet's screen at issue, kept forever with the prescription.</summary>
public sealed class PrescriptionWarning
{
    public Guid Id { get; private set; }
    public SafetyWarningKind Kind { get; private set; } = default!;
    public bool Blocking { get; private set; }
    public bool RequiresAcknowledgement { get; private set; }
    public InteractionSeverity? Severity { get; private set; }
    public string Subject { get; private set; } = default!;
    public string Detail { get; private set; } = default!;

    private PrescriptionWarning() { }

    public PrescriptionWarning(
        Guid id, SafetyWarningKind kind, bool blocking, bool requiresAcknowledgement, InteractionSeverity? severity, string subject, string detail)
    {
        Id = id;
        Kind = kind;
        Blocking = blocking;
        RequiresAcknowledgement = requiresAcknowledgement;
        Severity = severity;
        Subject = subject;
        Detail = detail;
    }
}

/// <summary>HO-PHM-001 §8.8 -- an owner's request for a repeat, owned by the prescription. <c>Requested</c> becomes <c>Dispensed</c>, <c>Declined</c> or <c>Withdrawn</c>.</summary>
public sealed class RepeatRequest
{
    public Guid Id { get; private set; }
    public Guid RequestedBy { get; private set; }
    public DateTimeOffset RequestedOn { get; private set; }
    public Guid PreferredBranchId { get; private set; }
    public string? Note { get; private set; }
    public RepeatRequestStatus Status { get; private set; } = default!;
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedOn { get; private set; }
    public string? DeclineReason { get; private set; }
    public Guid? DispensingId { get; private set; }

    private RepeatRequest() { }

    public RepeatRequest(Guid id, Guid requestedBy, DateTimeOffset requestedOn, Guid preferredBranchId, string? note)
    {
        Id = id;
        RequestedBy = requestedBy;
        RequestedOn = requestedOn;
        PreferredBranchId = preferredBranchId;
        Note = note;
        Status = RepeatRequestStatus.Requested;
    }

    public bool IsOpen => Status == RepeatRequestStatus.Requested;

    internal void MarkDispensed(Guid dispensingId, Guid decidedBy, DateTimeOffset now)
    {
        Status = RepeatRequestStatus.Dispensed;
        DispensingId = dispensingId;
        DecidedBy = decidedBy;
        DecidedOn = now;
    }

    internal void Decline(Guid decidedBy, string reason, DateTimeOffset now)
    {
        Status = RepeatRequestStatus.Declined;
        DeclineReason = reason;
        DecidedBy = decidedBy;
        DecidedOn = now;
    }

    internal void Withdraw(DateTimeOffset now)
    {
        Status = RepeatRequestStatus.Withdrawn;
        DecidedOn = now;
    }
}

/// <summary>HO-PHM-001 §8.7 -- one batch given: the exact batch, the quantity and the price in force then. The ledger entry that moved it is found by reference (dispensing id, line id) -- the stock port does not expose entry ids.</summary>
public sealed class DispensingLine
{
    public Guid Id { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid BatchId { get; private set; }
    public string LotNumber { get; private set; } = default!;
    public DateOnly? ExpiresOn { get; private set; }
    public decimal Quantity { get; private set; }
    public Guid? RegisterEntryId { get; private set; }
    public PriceSnapshot? PriceSnapshot { get; private set; }

    private DispensingLine() { }

    public DispensingLine(
        Guid id, Guid skuId, Guid batchId, string lotNumber, DateOnly? expiresOn, decimal quantity, Guid? registerEntryId,
        PriceSnapshot? priceSnapshot)
    {
        Id = id;
        SkuId = skuId;
        BatchId = batchId;
        LotNumber = lotNumber;
        ExpiresOn = expiresOn;
        Quantity = quantity;
        RegisterEntryId = registerEntryId;
        PriceSnapshot = priceSnapshot;
    }
}
