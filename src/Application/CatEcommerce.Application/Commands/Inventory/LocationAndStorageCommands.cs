using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Inventory;

// HO-IMP-001 §11.1 C1-C2 -- data only. Ids (including every line id) are minted in the controller; BranchId is the caller's
// ACTIVE branch (X-Branch-Id), never a body field. Enumerations travel as their display names and are checked by the validator.

/// <summary>C1 -- <c>StockLocations.Manage</c>.</summary>
public sealed class CreateStockLocationCommand : Command
{
    public required Guid LocationId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
}

public sealed class UpdateStockLocationCommand : Command
{
    public required Guid LocationId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Name { get; init; }
}

public sealed class SetStockLocationActivationCommand : Command
{
    public required Guid LocationId { get; init; }
    public required Guid BranchId { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>C2 -- <c>ProductStorage.Manage</c>. A storage profile is keyed by the product (1:1, created on first write).</summary>
public sealed class SetProductStorageProfileCommand : Command
{
    public required Guid ProductId { get; init; }
    public required string Condition { get; init; }
    public decimal? MinTemperatureC { get; init; }
    public decimal? MaxTemperatureC { get; init; }
    public string? HandlingNotes { get; init; }
}
