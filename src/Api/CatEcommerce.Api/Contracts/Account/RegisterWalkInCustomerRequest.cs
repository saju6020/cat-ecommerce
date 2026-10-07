namespace CatEcommerce.Api.Contracts.Account;

public sealed class RegisterWalkInCustomerRequest
{
    public required string FullName { get; init; }
    public required string MobileNumber { get; init; }
}
