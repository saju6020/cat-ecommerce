using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Cat;

public sealed class FlagCatHighRiskCommand : Command
{
    public required Guid CatId { get; init; }
    public required string HandlingNote { get; init; }
    public required string Reason { get; init; }
}
