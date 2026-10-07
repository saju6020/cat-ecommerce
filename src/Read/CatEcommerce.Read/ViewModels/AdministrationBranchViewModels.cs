namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-ADM-001 §12.4 -- times as "HH:mm", dates as "yyyy-MM-dd", instants ISO-8601 with offset.</summary>
public sealed record OpeningIntervalViewModel(string DayOfWeek, string Opens, string Closes);

public sealed record BranchSummaryViewModel(
    Guid Id, string Name, string? AddressLine, string? PhoneNumber, string? Email, string TimeZoneId,
    IReadOnlyList<OpeningIntervalViewModel> WeeklyHours);

public sealed record AdminBranchViewModel(
    Guid Id, string Code, string Name, string TimeZoneId, string? AddressLine, string? PhoneNumber,
    string? Email, bool IsActive, IReadOnlyList<OpeningIntervalViewModel> WeeklyHours);

public sealed record BranchClosureViewModel(
    Guid Id, Guid BranchId, DateOnly StartsOn, DateOnly EndsOn, string Kind, string Reason,
    string? PartialFrom, string? PartialTo, bool IsCancelled);

public sealed record RoomViewModel(Guid Id, Guid BranchId, string Name, string Kind, bool IsActive);

public sealed record TimeIntervalViewModel(DateTimeOffset Start, DateTimeOffset End);

public sealed record StaffBranchAssignmentViewModel(Guid StaffAccountId, IReadOnlyList<Guid> BranchIds, Guid PrimaryBranchId);
