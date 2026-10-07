namespace CatEcommerce.Api.Contracts.Cat;

/// <summary>US-CAT-01. Never carries an id, UserContext or CorrelationId -- the controller mints
/// and stamps those (HO-CAT-001 section 12.4).</summary>
public sealed class RegisterCatRequest
{
    public required Guid OwnerId { get; init; }
    public required string Name { get; init; }
    public string Species { get; init; } = "Cat";
    public required string Sex { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public required string DateOfBirthPrecision { get; init; }
    public string? Breed { get; init; }
    public string? ColourAndMarkings { get; init; }
}
