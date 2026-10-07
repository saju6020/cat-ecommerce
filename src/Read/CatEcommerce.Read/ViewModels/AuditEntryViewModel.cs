namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-ADM-001 §12.4 -- US-ADM-06.</summary>
public sealed record AuditEntryViewModel(
    DateTimeOffset OccurredOn, Guid ActorUserId, string ActorDisplayName, string Action, string Category,
    string EntityType, Guid EntityId, string Reason, string? BeforeValue, string? AfterValue, string? IpAddress);
