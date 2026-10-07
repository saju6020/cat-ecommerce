using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Cat;

public sealed class RecordCatNeuterStatusCommand : Command
{
    public required Guid CatId { get; init; }
    public required NeuterStatus NeuterStatus { get; init; }
    public DateOnly? NeuteredOn { get; init; }
}
