using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Cat;

public sealed class TransferCatOwnershipCommand : Command
{
    public required Guid CatId { get; init; }
    public required Guid NewOwnerId { get; init; }
    public required string Reason { get; init; }
}
