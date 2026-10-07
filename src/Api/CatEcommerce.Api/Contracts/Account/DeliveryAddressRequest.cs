namespace CatEcommerce.Api.Contracts.Account;

public sealed class DeliveryAddressRequest
{
    public required string Label { get; init; }
    public required string Line1 { get; init; }
    public string? Line2 { get; init; }
    public required string City { get; init; }
    public required string District { get; init; }
    public string? PostalCode { get; init; }
    public string? DeliveryInstructions { get; init; }
}
