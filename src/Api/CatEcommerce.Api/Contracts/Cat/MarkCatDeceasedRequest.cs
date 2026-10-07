namespace CatEcommerce.Api.Contracts.Cat;

public sealed class MarkCatDeceasedRequest
{
    public required DateOnly DeceasedOn { get; init; }
    public string? Note { get; init; }
}
