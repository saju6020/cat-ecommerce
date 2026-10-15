namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-XCT-001 section 12.4 Q1. One appointment of the day. The payload is minimal (NFR-SEC-8): <c>HandlingNote</c> only for a high-risk cat.</summary>
public sealed record SnapshotAppointmentItem(
    Guid AppointmentId, DateTimeOffset? StartsOn, DateTimeOffset? EndsOn, string Status, bool IsWalkIn, string? TriagePriority,
    string CatName, bool CatIsHighRisk, string? HandlingNote, string CustomerName, string? CustomerPhone, string ServiceName, string? PerformerName, string? RoomName);

public sealed record SnapshotAppointmentsSection(IReadOnlyList<SnapshotAppointmentItem> Items);

public sealed record SnapshotBranch(Guid Id, string Name, string TimeZoneId);

public sealed record SnapshotOmittedSection(string Section, string Reason);

/// <summary>The snapshot body, schema version 1.</summary>
public sealed record FrontDeskSnapshotBody(
    int SchemaVersion, SnapshotBranch Branch, string LocalDate, DateTimeOffset GeneratedOn, DateTimeOffset ValidUntil, Guid StaffAccountId,
    int IdleTimeoutMinutes, int RefreshAfterSeconds, IReadOnlyDictionary<string, object> Sections, IReadOnlyList<SnapshotOmittedSection> OmittedSections);

/// <summary>The snapshot with its strong ETag (SHA-256 of the canonical content, excluding <c>GeneratedOn</c>) and the item count per section.</summary>
public sealed record FrontDeskSnapshotViewModel(
    string ETag, DateOnly LocalDate, IReadOnlyDictionary<string, int> SectionCounts, FrontDeskSnapshotBody Body);
