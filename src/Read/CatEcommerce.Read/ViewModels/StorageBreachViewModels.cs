namespace CatEcommerce.Read.ViewModels;

// HO-IMP-001 section 8.13 / 12.4 -- storage-condition breaches (US-IMP-16 AC2).

public sealed record StorageBreachSummaryViewModel(
    Guid Id, Guid LocationId, string? LocationCode, DateTimeOffset ObservedOn, string Condition, string Status, int AffectedBatchCount, Guid ReportedBy, DateTimeOffset ReportedOn);

public sealed record StorageBreachBatchViewModel(Guid BatchId, string? SkuCode, string? DisplayName, string? LotNumber, string HeldAtLocation);

public sealed record StorageBreachDetailViewModel(
    StorageBreachSummaryViewModel Summary, decimal? TemperatureC, int? DurationMinutes, IReadOnlyList<StorageBreachBatchViewModel> AffectedBatches, DateTimeOffset? QuarantinedOn,
    Guid? QuarantinedBy, DateTimeOffset? ClosedOn, Guid? ClosedBy, string? ClosingNote, IReadOnlyList<string> AllowedActions);
