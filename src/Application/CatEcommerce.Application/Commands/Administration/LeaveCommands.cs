using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Administration;

public sealed class RequestLeaveCommand : Command
{
    public required Guid LeaveRequestId { get; init; }
    public Guid? StaffAccountId { get; init; }
    public required DateOnly StartsOn { get; init; }
    public required DateOnly EndsOn { get; init; }
    public required string Kind { get; init; }
    public string? Note { get; init; }
}

public sealed class ApproveLeaveCommand : Command
{
    public required Guid LeaveRequestId { get; init; }
    public string? DecisionNote { get; init; }
}

public sealed class RejectLeaveCommand : Command
{
    public required Guid LeaveRequestId { get; init; }
    public string? DecisionNote { get; init; }
}

public sealed class CancelLeaveCommand : Command
{
    public required Guid LeaveRequestId { get; init; }
}
