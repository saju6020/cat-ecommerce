namespace CatEcommerce.Api.Contracts.Cat;

public sealed class ClearCatHighRiskFlagRequest
{
    public required string Reason { get; init; }
}
