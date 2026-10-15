using CatEcommerce.Domain.Events.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>
/// HO-IMP-001 §8.13 (US-IMP-16 AC2, ADR-115). A reported storage-condition breach at a location: flags the batches held there and can
/// move them to quarantine pending a decision -- a LOCATION-level quarantine (stock transferred to the branch's quarantine location),
/// distinct from batch-level <c>Quarantined</c>. <c>Open -> Quarantined -> Closed</c>; <c>Open -> Closed</c>.
/// <see cref="AffectedBatchIds"/> is a snapshot of the batches with stock there when reported.
/// </summary>
public sealed class StorageBreachReportAggregate : AggregateRoot
{
    private readonly List<Guid> _affectedBatchIds = [];

    public Guid BranchId { get; private set; }
    public Guid LocationId { get; private set; }
    public DateTimeOffset ObservedOn { get; private set; }
    public string Condition { get; private set; } = default!;
    public decimal? TemperatureC { get; private set; }
    public int? DurationMinutes { get; private set; }
    public IReadOnlyList<Guid> AffectedBatchIds => _affectedBatchIds;
    public StorageBreachStatus Status { get; private set; } = StorageBreachStatus.Open;
    public Guid ReportedBy { get; private set; }
    public DateTimeOffset ReportedOn { get; private set; }
    public DateTimeOffset? QuarantinedOn { get; private set; }
    public Guid? QuarantinedBy { get; private set; }
    public DateTimeOffset? ClosedOn { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public string? ClosingNote { get; private set; }

    private StorageBreachReportAggregate() { }

    public StorageBreachReportAggregate(Guid id) : base(id) { }

    /// <summary>The batches a breach affects: those with stock on hand at the location (a zero position is not affected) -- U-J1.</summary>
    public static IReadOnlyList<Guid> AffectedBatches(IEnumerable<StockPositionRow> positionsAtLocation) =>
        positionsAtLocation.Where(p => p.OnHand > 0m).Select(p => p.BatchId).Distinct().ToList();

    public void Report(StorageBreachReportDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Condition) || dto.Condition.Trim().Length > 500)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidStorageBreachTransition, "Describe the breach in at most 500 characters.", nameof(Condition));
        if (dto.TemperatureC is < -100m or > 200m || dto.DurationMinutes is < 0)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidTemperatureRange, "The temperature or duration is out of range.", nameof(TemperatureC));
        if (dto.AffectedBatchIds.Count == 0)
            throw InventoryViolation.Of(BusinessViolationCodes.NoStockAtLocation, "No stock is held at that location, so there is nothing to flag.", nameof(AffectedBatchIds));
        if (dto.AffectedBatchIds.Count > InventoryRules.MaxBreachBatches)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidStorageBreachTransition, $"At most {InventoryRules.MaxBreachBatches} batches can be flagged.", nameof(AffectedBatchIds));

        BranchId = dto.BranchId;
        LocationId = dto.LocationId;
        ObservedOn = dto.ObservedOn;
        Condition = dto.Condition.Trim();
        TemperatureC = dto.TemperatureC;
        DurationMinutes = dto.DurationMinutes;
        _affectedBatchIds.Clear();
        _affectedBatchIds.AddRange(dto.AffectedBatchIds.Distinct());
        Status = StorageBreachStatus.Open;
        ReportedBy = dto.Actor.UserContext.UserId;
        ReportedOn = dto.ReportedOn;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new StorageBreachReportedEvent { BreachId = Id, LocationId = LocationId, AffectedBatchCount = _affectedBatchIds.Count, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    public void MarkQuarantined(DateTimeOffset on, InventoryActor actor)
    {
        if (Status != StorageBreachStatus.Open)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidStorageBreachTransition, $"A {Status.DisplayName} breach cannot be quarantined.", nameof(Status));

        Status = StorageBreachStatus.Quarantined;
        QuarantinedOn = on;
        QuarantinedBy = actor.UserContext.UserId;
        Changed(actor);
    }

    /// <summary>Records the decision taken afterwards (a transfer back or a write-off is an ordinary movement).</summary>
    public void Close(string? note, DateTimeOffset on, InventoryActor actor)
    {
        if (Status == StorageBreachStatus.Closed)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidStorageBreachTransition, "This breach is already closed.", nameof(Status));
        var text = InventoryViolation.Normalise(note);
        if (text.Length < InventoryRules.ReasonMinLength || text.Length > InventoryRules.NoteMaxLength)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidStorageBreachTransition, "Record the decision in 5 to 500 characters.", nameof(note));

        Status = StorageBreachStatus.Closed;
        ClosedOn = on;
        ClosedBy = actor.UserContext.UserId;
        ClosingNote = text;
        Changed(actor);
    }

    private void Changed(InventoryActor actor)
    {
        AddDomainEvent(new StorageBreachChangedEvent { BreachId = Id, Status = Status, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }
}
