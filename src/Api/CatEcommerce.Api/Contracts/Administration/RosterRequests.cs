namespace CatEcommerce.Api.Contracts.Administration;

public sealed class ShiftSlotRequest
{
    public string DayOfWeek { get; init; } = default!;
    public string Starts { get; init; } = default!;
    public string Ends { get; init; } = default!;
    public Guid? DefaultRoomId { get; init; }
}

public sealed class SetRosterPatternRequest
{
    public IReadOnlyList<ShiftSlotRequest> Slots { get; init; } = [];
    public DateOnly? EffectiveFrom { get; init; }
}

public sealed class CreateRosterExceptionRequest
{
    public string Kind { get; init; } = default!;
    public DateOnly Date { get; init; }
    public string Starts { get; init; } = default!;
    public string Ends { get; init; } = default!;
    public string Reason { get; init; } = default!;
}

public sealed class RequestLeaveRequest
{
    public Guid? StaffAccountId { get; init; }
    public DateOnly StartsOn { get; init; }
    public DateOnly EndsOn { get; init; }
    public string Kind { get; init; } = default!;
    public string? Note { get; init; }
}

public sealed class LeaveDecisionRequest
{
    public string? DecisionNote { get; init; }
}
