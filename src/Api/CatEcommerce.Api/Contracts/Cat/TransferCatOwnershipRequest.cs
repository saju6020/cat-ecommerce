namespace CatEcommerce.Api.Contracts.Cat;

public sealed class TransferCatOwnershipRequest
{
    public required Guid NewOwnerId { get; init; }
    public required string Reason { get; init; }
}
