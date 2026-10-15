namespace CatEcommerce.Read.ViewModels;

// HO-BRD-001 section 12.4 (Phase E) -- the day task list (NFR-AVL-3's input) and observations.

/// <summary><see cref="IsOverdue"/> is derived at read time (pending and past due plus the grace), never stored (US-BRD-10 AC2).</summary>
public sealed record CareTaskViewModel(
    Guid Id, Guid StayId, Guid CatId, string CatName, bool CatIsHighRisk, string? HandlingNote, string Kind, string Title, DateTimeOffset DueOn, string DueLocalTime,
    string Status, bool IsOverdue, Guid? AssignedCaretakerId, string? AssignedCaretakerName, DateTimeOffset? CompletedOn, Guid? CompletedBy, string? Note, string? SkipReason);

public sealed record CareTaskUnitGroupViewModel(Guid UnitId, string Code, string Name, string UnitType, string? Block, IReadOnlyList<CareTaskViewModel> Tasks);

/// <summary>The day's tasks grouped by unit. <see cref="Unassigned"/> counts pending tasks nobody is rostered for.</summary>
public sealed record CareTaskDayListViewModel(DateOnly Date, string BranchTimeZoneId, int Total, int Pending, int Overdue, int Unassigned, IReadOnlyList<CareTaskUnitGroupViewModel> Units);

/// <summary>A checked-in stay and the branch-local date it is today -- the worker generates today's and tomorrow's tasks from it.</summary>
public sealed record InHouseStayViewModel(Guid StayId, DateOnly LocalToday);

public sealed record CareObservationViewModel(
    Guid Id, Guid CatId, string CatName, DateTimeOffset RecordedOn, Guid RecordedBy, string PortionEaten, string WaterIntake, string LitterOutput, string Mood, string? Notes);

/// <summary><see cref="NotEating"/> lists the cats whose last TWO observations both recorded nothing eaten (US-BRD-11 AC2) -- derived, never stored.</summary>
public sealed record StayObservationsViewModel(Guid StayId, IReadOnlyList<Guid> NotEating, IReadOnlyList<CareObservationViewModel> Items);
