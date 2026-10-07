using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Administration;

public sealed class CreateRoomCommand : Command
{
    public required Guid RoomId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
}

public sealed class UpdateRoomCommand : Command
{
    public required Guid RoomId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required bool IsActive { get; init; }
}
