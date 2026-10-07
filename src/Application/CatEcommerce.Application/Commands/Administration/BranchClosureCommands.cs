using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Administration;

public sealed class CreateBranchClosureCommand : Command
{
    public required Guid ClosureId { get; init; }
    public required Guid BranchId { get; init; }
    public required DateOnly StartsOn { get; init; }
    public required DateOnly EndsOn { get; init; }
    public required string Kind { get; init; }
    public required string Reason { get; init; }
    public TimeOnly? PartialFrom { get; init; }
    public TimeOnly? PartialTo { get; init; }
}

public sealed class CancelBranchClosureCommand : Command
{
    public required Guid BranchId { get; init; }
    public required Guid ClosureId { get; init; }
}
