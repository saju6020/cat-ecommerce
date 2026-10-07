namespace CatEcommerce.Api.Contracts.Cat;

public sealed class UpdateCatProfileRequest
{
    public required string Name { get; init; }
    public required string Sex { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public required string DateOfBirthPrecision { get; init; }
    public string? Breed { get; init; }
    public string? ColourAndMarkings { get; init; }
}
