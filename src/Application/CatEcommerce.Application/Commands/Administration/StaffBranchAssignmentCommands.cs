using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Administration;

public sealed class AssignStaffBranchesCommand : Command
{
    public required Guid StaffAccountId { get; init; }
    public required IReadOnlyList<Guid> BranchIds { get; init; }
    public required Guid PrimaryBranchId { get; init; }
}
