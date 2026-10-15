using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Inventory;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>
/// HO-IMP-001 §8.5 (US-IMP-03, US-IMP-05). An import consignment: origin, shipment reference, HS codes, permit, health /
/// veterinary certificate, customs declaration, cost components, exchange rates and document METADATA (never bytes, ADR-113).
/// <c>Planned -> InTransit -> AtCustoms -> Cleared</c>; any non-cleared status -> <c>Cancelled</c>. A cancelled consignment is
/// kept, with its documents (NFR-LEG-2): there is no method that removes a document or a consignment.
/// The "POs are Issued/Closed and of this branch" rule needs the POs, so the handler checks it.
/// </summary>
public sealed class ConsignmentAggregate : AggregateRoot
{
    private static readonly IReadOnlyDictionary<ConsignmentStatus, ConsignmentStatus> Next = new Dictionary<ConsignmentStatus, ConsignmentStatus>
    {
        [ConsignmentStatus.Planned] = ConsignmentStatus.InTransit,
        [ConsignmentStatus.InTransit] = ConsignmentStatus.AtCustoms,
        [ConsignmentStatus.AtCustoms] = ConsignmentStatus.Cleared,
    };

    private readonly List<Guid> _purchaseOrderIds = [];
    private readonly List<ConsignmentHsCode> _hsCodes = [];
    private readonly List<ConsignmentCostComponent> _costComponents = [];
    private readonly List<ConsignmentExchangeRate> _exchangeRates = [];
    private readonly List<ConsignmentDocument> _documents = [];
    private readonly List<LandedCostLine> _landedCost = [];
    private readonly List<ConsignmentStatusChange> _statusHistory = [];

    public string Reference { get; private set; } = default!;
    public Guid BranchId { get; private set; }
    public string OriginCountryCode { get; private set; } = default!;
    public string ShipmentReference { get; private set; } = default!;
    public IReadOnlyList<Guid> PurchaseOrderIds => _purchaseOrderIds;
    public string? ImportPermitNumber { get; private set; }
    public string? HealthCertificateNumber { get; private set; }
    public string? CustomsDeclarationNumber { get; private set; }
    public DateOnly? ExpectedArrivalOn { get; private set; }
    public ConsignmentStatus Status { get; private set; } = ConsignmentStatus.Planned;
    public string? CancellationReason { get; private set; }
    public DateOnly? ClearedOn { get; private set; }
    public DateTimeOffset? AllocatedOn { get; private set; }
    public Guid? AllocatedBy { get; private set; }
    public bool CostsChangedSinceAllocation { get; private set; }

    public IReadOnlyList<ConsignmentHsCode> HsCodes => _hsCodes;
    public IReadOnlyList<ConsignmentCostComponent> CostComponents => _costComponents;
    public IReadOnlyList<ConsignmentExchangeRate> ExchangeRates => _exchangeRates;
    public IReadOnlyList<ConsignmentDocument> Documents => _documents;
    public IReadOnlyList<LandedCostLine> LandedCost => _landedCost;
    public IReadOnlyList<ConsignmentStatusChange> StatusHistory => _statusHistory;

    private ConsignmentAggregate() { }

    public ConsignmentAggregate(Guid id) : base(id) { }

    public void Plan(ConsignmentPlanDto dto)
    {
        Reference = dto.Reference;
        BranchId = dto.BranchId;
        Status = ConsignmentStatus.Planned;
        ApplyDetails(dto.Details);

        SetDefaultValue(dto.Details.Actor.UserContext);
        AddDomainEvent(new ConsignmentPlannedEvent { ConsignmentId = Id, BranchId = BranchId, CorrelationId = dto.Details.Actor.CorrelationId, UserContext = dto.Details.Actor.UserContext });
        UpdateVersion();
    }

    public void UpdateDetails(ConsignmentDetailsDto dto)
    {
        RequireNotTerminal();
        ApplyDetails(dto);

        AddDomainEvent(new ConsignmentUpdatedEvent { ConsignmentId = Id, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    /// <summary>The strict sequence <c>Planned -> InTransit -> AtCustoms -> Cleared</c>. Clearing needs the three document numbers.</summary>
    public void AdvanceStatus(ConsignmentStatus to, DateOnly on, DateTimeOffset changedOn, InventoryActor actor)
    {
        if (!Next.TryGetValue(Status, out var expected) || expected != to)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidConsignmentTransition, $"A consignment cannot move from {Status.DisplayName} to {to.DisplayName}.", nameof(Status));

        if (to == ConsignmentStatus.Cleared &&
            (string.IsNullOrWhiteSpace(ImportPermitNumber) || string.IsNullOrWhiteSpace(HealthCertificateNumber) || string.IsNullOrWhiteSpace(CustomsDeclarationNumber)))
            throw InventoryViolation.Of(BusinessViolationCodes.ClearanceDocumentsMissing, "The import permit, health certificate and customs declaration numbers are required to clear a consignment.", nameof(ImportPermitNumber));

        Transition(to, on, changedOn, actor);
        if (to == ConsignmentStatus.Cleared) ClearedOn = on;
    }

    public void Cancel(string? reason, DateTimeOffset changedOn, InventoryActor actor)
    {
        if (Status == ConsignmentStatus.Cleared || Status == ConsignmentStatus.Cancelled)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidConsignmentTransition, $"A {Status.DisplayName} consignment cannot be cancelled.", nameof(Status));
        var text = InventoryViolation.Normalise(reason);
        if (text.Length == 0 || text.Length > 500)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidConsignmentTransition, "A cancellation reason (at most 500 characters) is required.", "Reason");

        CancellationReason = text;
        Transition(ConsignmentStatus.Cancelled, DateOnly.FromDateTime(changedOn.UtcDateTime), changedOn, actor);
    }

    public void AddDocument(ConsignmentDocumentDto dto)
    {
        RequireNotCancelled();
        _documents.Add(BuildDocument(dto));

        AddDomainEvent(new ConsignmentDocumentAddedEvent { ConsignmentId = Id, DocumentId = dto.DocumentId, Kind = dto.Kind, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    /// <summary>The old document stays (flagged superseded); the new one is added. There is no remove method (NFR-LEG-2).</summary>
    public void SupersedeDocument(Guid documentId, ConsignmentDocumentDto replacement)
    {
        RequireNotCancelled();
        var old = _documents.FirstOrDefault(d => d.Id == documentId)
            ?? throw InventoryViolation.Of(BusinessViolationCodes.ConsignmentDocumentNotFound, "That document is not on this consignment.", nameof(documentId));
        if (old.SupersededOn is not null)
            throw InventoryViolation.Of(BusinessViolationCodes.ConsignmentDocumentNotFound, "That document has already been superseded.", nameof(documentId));

        var added = BuildDocument(replacement);
        old.MarkSuperseded(replacement.UploadedOn, replacement.Actor.UserContext.UserId);
        _documents.Add(added);

        AddDomainEvent(new ConsignmentDocumentAddedEvent { ConsignmentId = Id, DocumentId = replacement.DocumentId, Kind = replacement.Kind, CorrelationId = replacement.Actor.CorrelationId, UserContext = replacement.Actor.UserContext });
        UpdateVersion();
    }

    /// <summary>Replaces the costs and rates wholesale. When an allocation already exists it becomes STALE (§8.11).</summary>
    public void ReplaceCostsAndRates(ConsignmentCostsDto dto)
    {
        RequireNotCancelled();
        if (dto.CostComponents.Count > InventoryRules.MaxCostComponents || dto.ExchangeRates.Count > InventoryRules.MaxExchangeRates)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidExchangeRate, "Too many cost components or exchange rates.", nameof(dto.CostComponents));

        foreach (var (component, i) in dto.CostComponents.Select((c, i) => (c, i)))
        {
            if (component.Amount < 0m)
                throw InventoryViolation.Of(BusinessViolationCodes.InvalidExchangeRate, "A cost amount cannot be negative.", $"CostComponents[{i}].Amount");
            if (!InventoryRules.IsValidCurrencyCode(component.CurrencyCode))
                throw InventoryViolation.Of(BusinessViolationCodes.InvalidCurrencyCode, "The currency must be a three-letter ISO-4217 code in upper case.", $"CostComponents[{i}].CurrencyCode");
            if (component.Note is { Length: > 200 })
                throw InventoryViolation.Of(BusinessViolationCodes.InvalidExchangeRate, "A cost note cannot exceed 200 characters.", $"CostComponents[{i}].Note");
        }

        foreach (var (rate, i) in dto.ExchangeRates.Select((r, i) => (r, i)))
        {
            if (rate.RateToBase <= 0m || decimal.Round(rate.RateToBase, 6) != rate.RateToBase)
                throw InventoryViolation.Of(BusinessViolationCodes.InvalidExchangeRate, "An exchange rate must be greater than zero with at most six decimal places.", $"ExchangeRates[{i}].RateToBase");
            if (!InventoryRules.IsValidCurrencyCode(rate.CurrencyCode))
                throw InventoryViolation.Of(BusinessViolationCodes.InvalidCurrencyCode, "The currency must be a three-letter ISO-4217 code in upper case.", $"ExchangeRates[{i}].CurrencyCode");
        }

        if (dto.ExchangeRates.Select(r => r.CurrencyCode).Distinct().Count() != dto.ExchangeRates.Count)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidExchangeRate, "There can be only one exchange rate per currency.", nameof(dto.ExchangeRates));

        _costComponents.Clear();
        _costComponents.AddRange(dto.CostComponents.Select(c => new ConsignmentCostComponent(Guid.NewGuid(), c.Kind, decimal.Round(c.Amount, 2), c.CurrencyCode, c.Note)));
        _exchangeRates.Clear();
        _exchangeRates.AddRange(dto.ExchangeRates.Select(r => new ConsignmentExchangeRate(Guid.NewGuid(), r.CurrencyCode, r.RateToBase, r.RecordedOn)));

        if (AllocatedOn is not null) CostsChangedSinceAllocation = true;

        AddDomainEvent(new ConsignmentCostsReplacedEvent { ConsignmentId = Id, AllocationNowStale = CostsChangedSinceAllocation, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    /// <summary>Replaces the allocation lines (re-allocation is allowed at any time) and clears the stale flag.</summary>
    public void RecordLandedCostAllocation(LandedCostAllocationDto dto)
    {
        RequireNotCancelled();
        if (dto.Lines.Count == 0 || dto.Lines.Count > InventoryRules.MaxLandedCostLines)
            throw InventoryViolation.Of(BusinessViolationCodes.NoReceivedQuantityToAllocate, "There is no received quantity on this consignment to allocate costs to.", nameof(dto.Lines));

        _landedCost.Clear();
        _landedCost.AddRange(dto.Lines.Select(l => new LandedCostLine(Guid.NewGuid(), l.BatchId, l.Quantity, l.PurchaseCostBase, l.AllocatedCostBase, l.LandedUnitCostBase)));
        AllocatedOn = dto.AllocatedOn;
        AllocatedBy = dto.Actor.UserContext.UserId;
        CostsChangedSinceAllocation = false;

        AddDomainEvent(new LandedCostAllocatedEvent { ConsignmentId = Id, BatchCount = dto.Lines.Count, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    private void Transition(ConsignmentStatus to, DateOnly effectiveOn, DateTimeOffset changedOn, InventoryActor actor)
    {

        _statusHistory.Add(new ConsignmentStatusChange(Guid.NewGuid(), Status, to, changedOn, actor.UserContext.UserId, effectiveOn, _statusHistory.Count + 1));
        var from = Status;
        Status = to;

        AddDomainEvent(new ConsignmentStatusChangedEvent { ConsignmentId = Id, From = from, To = to, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    private void ApplyDetails(ConsignmentDetailsDto dto)
    {
        if (!InventoryRules.IsValidCountryCode(dto.OriginCountryCode))
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidCountryCode, "The origin must be a valid ISO-3166 two-letter code.", nameof(OriginCountryCode));
        if (string.IsNullOrWhiteSpace(dto.ShipmentReference) || dto.ShipmentReference.Trim().Length > 60)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidConsignment, "A shipment reference (bill of lading or air waybill) of at most 60 characters is required.", nameof(ShipmentReference));
        if (dto.PurchaseOrderIds.Count is 0 or > InventoryRules.MaxConsignmentPurchaseOrders || dto.PurchaseOrderIds.Distinct().Count() != dto.PurchaseOrderIds.Count)
            throw InventoryViolation.Of(BusinessViolationCodes.ConsignmentPurchaseOrderMismatch, $"A consignment groups between 1 and {InventoryRules.MaxConsignmentPurchaseOrders} distinct purchase orders.", nameof(PurchaseOrderIds));
        if (dto.HsCodes.Count > InventoryRules.MaxHsCodes)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidHsCode, $"A consignment has at most {InventoryRules.MaxHsCodes} HS codes.", nameof(HsCodes));

        for (var i = 0; i < dto.HsCodes.Count; i++)
        {
            if (!InventoryRules.IsValidHsCode(dto.HsCodes[i].Code))
                throw InventoryViolation.Of(BusinessViolationCodes.InvalidHsCode, "An HS code is four digits followed by up to three groups of two (for example 2309.10).", $"HsCodes[{i}].Code");
            if (dto.HsCodes[i].Description is { Length: > 200 })
                throw InventoryViolation.Of(BusinessViolationCodes.InvalidHsCode, "An HS code description cannot exceed 200 characters.", $"HsCodes[{i}].Description");
        }

        foreach (var number in new[] { dto.ImportPermitNumber, dto.HealthCertificateNumber, dto.CustomsDeclarationNumber })
        {
            if (number is { Length: > 60 })
                throw InventoryViolation.Of(BusinessViolationCodes.InvalidConsignment, "A permit, certificate or declaration number cannot exceed 60 characters.", nameof(ImportPermitNumber));
        }

        OriginCountryCode = dto.OriginCountryCode;
        ShipmentReference = dto.ShipmentReference.Trim();
        ImportPermitNumber = Blank(dto.ImportPermitNumber);
        HealthCertificateNumber = Blank(dto.HealthCertificateNumber);
        CustomsDeclarationNumber = Blank(dto.CustomsDeclarationNumber);
        ExpectedArrivalOn = dto.ExpectedArrivalOn;

        _purchaseOrderIds.Clear();
        _purchaseOrderIds.AddRange(dto.PurchaseOrderIds);
        _hsCodes.Clear();
        _hsCodes.AddRange(dto.HsCodes.Select(h => new ConsignmentHsCode(Guid.NewGuid(), h.Code.Trim(), string.IsNullOrWhiteSpace(h.Description) ? null : h.Description.Trim())));
    }

    private ConsignmentDocument BuildDocument(ConsignmentDocumentDto dto)
    {
        if (_documents.Count >= InventoryRules.MaxDocuments)
            throw InventoryViolation.Of(BusinessViolationCodes.DocumentLimitExceeded, $"A consignment holds at most {InventoryRules.MaxDocuments} documents.", nameof(Documents));
        if (!InventoryRules.AllowedDocumentContentTypes.Contains(dto.ContentType))
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidDocumentContentType, "A document must be a PDF, JPEG or PNG file.", nameof(dto.ContentType));
        if (dto.SizeInBytes <= 0 || dto.SizeInBytes > InventoryRules.MaxDocumentBytes)
            throw InventoryViolation.Of(BusinessViolationCodes.DocumentTooLarge, "A document cannot be larger than 20 MB.", nameof(dto.SizeInBytes));
        if (string.IsNullOrWhiteSpace(dto.FileName) || dto.FileName.Length > 255 || string.IsNullOrWhiteSpace(dto.StorageKey) || dto.StorageKey.Length > 500)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidConsignment, "A document needs a file name (at most 255) and a storage key (at most 500).", nameof(dto.FileName));
        if (dto.DocumentNumber is { Length: > 60 })
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidConsignment, "A document number cannot exceed 60 characters.", nameof(dto.DocumentNumber));

        return new ConsignmentDocument(
            dto.DocumentId, dto.Kind, Blank(dto.DocumentNumber), dto.IssuedOn, dto.FileName, dto.StorageKey, dto.ContentType,
            dto.SizeInBytes, dto.Actor.UserContext.UserId, dto.UploadedOn);
    }

    private void RequireNotTerminal()
    {
        if (Status == ConsignmentStatus.Cleared || Status == ConsignmentStatus.Cancelled)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidConsignmentTransition, $"A {Status.DisplayName} consignment can no longer be edited.", nameof(Status));
    }

    private void RequireNotCancelled()
    {
        if (Status == ConsignmentStatus.Cancelled)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidConsignmentTransition, "A cancelled consignment can no longer be edited.", nameof(Status));
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
