using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Cat;

public sealed class UpdateCatProfileCommand : Command
{
    public required Guid CatId { get; init; }
    public required string Name { get; init; }
    public required CatSex Sex { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public required DateOfBirthPrecision DateOfBirthPrecision { get; init; }
    public string? Breed { get; init; }
    public string? ColourAndMarkings { get; init; }
}
