using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

// HO-IMP-001 §8.5 -- everything owned by ConsignmentAggregate. Documents are NEVER removed (NFR-LEG-2, ADR-113).

public sealed class ConsignmentHsCode
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = default!;
    public string? Description { get; private set; }

    private ConsignmentHsCode() { }

    public ConsignmentHsCode(Guid id, string code, string? description)
    {
        Id = id;
        Code = code;
        Description = description;
    }
}

public sealed class ConsignmentCostComponent
{
    public Guid Id { get; private set; }
    public CostComponentKind Kind { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public string? Note { get; private set; }

    private ConsignmentCostComponent() { }

    public ConsignmentCostComponent(Guid id, CostComponentKind kind, decimal amount, string currencyCode, string? note)
    {
        Id = id;
        Kind = kind;
        Amount = amount;
        CurrencyCode = currencyCode;
        Note = note;
    }
}

public sealed class ConsignmentExchangeRate
{
    public Guid Id { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public decimal RateToBase { get; private set; }
    public DateOnly RecordedOn { get; private set; }

    private ConsignmentExchangeRate() { }

    public ConsignmentExchangeRate(Guid id, string currencyCode, decimal rateToBase, DateOnly recordedOn)
    {
        Id = id;
        CurrencyCode = currencyCode;
        RateToBase = rateToBase;
        RecordedOn = recordedOn;
    }
}

/// <summary>File METADATA and a storage key -- never bytes (§4.3, ADR-113). Superseded, never removed.</summary>
public sealed class ConsignmentDocument
{
    public Guid Id { get; private set; }
    public ImportDocumentKind Kind { get; private set; } = default!;
    public string? DocumentNumber { get; private set; }
    public DateOnly? IssuedOn { get; private set; }
    public string FileName { get; private set; } = default!;
    public string StorageKey { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeInBytes { get; private set; }
    public Guid UploadedBy { get; private set; }
    public DateTimeOffset UploadedOn { get; private set; }
    public DateTimeOffset? SupersededOn { get; private set; }
    public Guid? SupersededBy { get; private set; }

    private ConsignmentDocument() { }

    public ConsignmentDocument(
        Guid id, ImportDocumentKind kind, string? documentNumber, DateOnly? issuedOn, string fileName, string storageKey,
        string contentType, long sizeInBytes, Guid uploadedBy, DateTimeOffset uploadedOn)
    {
        Id = id;
        Kind = kind;
        DocumentNumber = documentNumber;
        IssuedOn = issuedOn;
        FileName = fileName;
        StorageKey = storageKey;
        ContentType = contentType;
        SizeInBytes = sizeInBytes;
        UploadedBy = uploadedBy;
        UploadedOn = uploadedOn;
    }

    /// <summary>Internal to the aggregate: only <c>ConsignmentAggregate.SupersedeDocument</c> calls it.</summary>
    internal void MarkSuperseded(DateTimeOffset on, Guid by)
    {
        SupersededOn = on;
        SupersededBy = by;
    }
}

public sealed class LandedCostLine
{
    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal PurchaseCostBase { get; private set; }
    public decimal AllocatedCostBase { get; private set; }
    public decimal LandedUnitCostBase { get; private set; }

    private LandedCostLine() { }

    public LandedCostLine(Guid id, Guid batchId, decimal quantity, decimal purchaseCostBase, decimal allocatedCostBase, decimal landedUnitCostBase)
    {
        Id = id;
        BatchId = batchId;
        Quantity = quantity;
        PurchaseCostBase = purchaseCostBase;
        AllocatedCostBase = allocatedCostBase;
        LandedUnitCostBase = landedUnitCostBase;
    }
}

public sealed class ConsignmentStatusChange
{
    public Guid Id { get; private set; }
    public ConsignmentStatus From { get; private set; } = default!;
    public ConsignmentStatus To { get; private set; } = default!;
    public DateTimeOffset ChangedOn { get; private set; }
    public Guid ChangedBy { get; private set; }
    public DateOnly? EffectiveOn { get; private set; }
    public int Sequence { get; private set; }

    private ConsignmentStatusChange() { }

    public ConsignmentStatusChange(Guid id, ConsignmentStatus from, ConsignmentStatus to, DateTimeOffset changedOn, Guid changedBy, DateOnly? effectiveOn, int sequence)
    {
        Id = id;
        From = from;
        To = to;
        ChangedOn = changedOn;
        ChangedBy = changedBy;
        EffectiveOn = effectiveOn;
        Sequence = sequence;
    }
}
