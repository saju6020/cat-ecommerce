namespace CatEcommerce.Read.ViewModels;

public sealed record ShiftSlotViewModel(string DayOfWeek, string Starts, string Ends, Guid? DefaultRoomId);

public sealed record StaffRosterViewModel(
    Guid StaffAccountId, Guid BranchId, IReadOnlyList<ShiftSlotViewModel> CurrentPattern,
    IReadOnlyList<ShiftSlotViewModel>? NextPattern, DateOnly? NextPatternEffectiveFrom);

public sealed record RosterExceptionViewModel(
    Guid Id, Guid StaffAccountId, Guid BranchId, DateOnly Date, string Kind, string Starts, string Ends,
    string Reason, bool IsCancelled);

public sealed record StaffAvailableIntervalViewModel(
    Guid StaffAccountId, Guid BranchId, DateTimeOffset Start, DateTimeOffset End, Guid? DefaultRoomId, bool OutsideOpeningHours);

public sealed record LeaveRequestViewModel(
    Guid Id, Guid StaffAccountId, DateOnly StartsOn, DateOnly EndsOn, string Kind, string? Note, string Status,
    Guid? DecidedBy, DateTimeOffset? DecidedOn, string? DecisionNote);
