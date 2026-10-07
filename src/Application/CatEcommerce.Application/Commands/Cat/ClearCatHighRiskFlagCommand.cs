using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Cat;

public sealed class ClearCatHighRiskFlagCommand : Command
{
    public required Guid CatId { get; init; }
    public required string Reason { get; init; }
}
