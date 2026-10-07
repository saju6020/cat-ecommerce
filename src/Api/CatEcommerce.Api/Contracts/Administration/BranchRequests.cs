namespace CatEcommerce.Api.Contracts.Administration;

public sealed class CreateBranchRequest
{
    public string Code { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string TimeZoneId { get; init; } = default!;
    public string? AddressLine { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
}

public sealed class UpdateBranchDetailsRequest
{
    public string Name { get; init; } = default!;
    public string TimeZoneId { get; init; } = default!;
    public string? AddressLine { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
}

public sealed class OpeningIntervalRequest
{
    public string DayOfWeek { get; init; } = default!;
    public string Opens { get; init; } = default!;
    public string Closes { get; init; } = default!;
}

public sealed class SetBranchWeeklyHoursRequest
{
    public IReadOnlyList<OpeningIntervalRequest> Intervals { get; init; } = [];
}

public sealed class SetBranchActivationRequest
{
    public bool IsActive { get; init; }
}

public sealed class CreateBranchClosureRequest
{
    public DateOnly StartsOn { get; init; }
    public DateOnly EndsOn { get; init; }
    public string Kind { get; init; } = default!;
    public string Reason { get; init; } = default!;
    public string? PartialFrom { get; init; }
    public string? PartialTo { get; init; }
}

public sealed class CreateRoomRequest
{
    public string Name { get; init; } = default!;
    public string Kind { get; init; } = default!;
}

public sealed class UpdateRoomRequest
{
    public string Name { get; init; } = default!;
    public string Kind { get; init; } = default!;
    public bool IsActive { get; init; }
}

public sealed class AssignStaffBranchesRequest
{
    public IReadOnlyList<Guid> BranchIds { get; init; } = [];
    public Guid PrimaryBranchId { get; init; }
}
