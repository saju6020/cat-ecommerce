namespace CatEcommerce.Api.Contracts.Cat;

public sealed class FlagCatHighRiskRequest
{
    public required string HandlingNote { get; init; }
    public required string Reason { get; init; }
}
