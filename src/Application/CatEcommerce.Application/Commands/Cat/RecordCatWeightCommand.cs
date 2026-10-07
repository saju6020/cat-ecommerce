using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Cat;

public sealed class RecordCatWeightCommand : Command
{
    public required Guid WeightRecordId { get; init; }
    public required Guid CatId { get; init; }
    public required decimal Kilograms { get; init; }
    public required DateOnly RecordedOn { get; init; }
    public required Guid RecordedByStaffId { get; init; }
    public string? Notes { get; init; }
}
