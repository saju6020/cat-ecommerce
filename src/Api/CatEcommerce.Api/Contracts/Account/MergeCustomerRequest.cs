namespace CatEcommerce.Api.Contracts.Account;

public sealed class MergeCustomerRequest
{
    public required Guid SurvivingCustomerId { get; init; }
    public required string Reason { get; init; }
}
