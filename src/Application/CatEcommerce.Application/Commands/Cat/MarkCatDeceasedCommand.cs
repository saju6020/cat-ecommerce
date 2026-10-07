using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Cat;

public sealed class MarkCatDeceasedCommand : Command
{
    public required Guid CatId { get; init; }
    public required DateOnly DeceasedOn { get; init; }
    public string? Note { get; init; }
}
