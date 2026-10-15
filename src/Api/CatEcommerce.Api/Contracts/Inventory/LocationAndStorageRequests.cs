namespace CatEcommerce.Api.Contracts.Inventory;

/// <summary>HO-IMP-001 §12.4 request bodies -- locations and storage profiles.</summary>
public sealed class CreateStockLocationRequest
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
}

public sealed class UpdateStockLocationRequest
{
    public required string Name { get; init; }
}

public sealed class SetStockLocationActivationRequest
{
    public required bool IsActive { get; init; }
}

public sealed class SetProductStorageProfileRequest
{
    public required string Condition { get; init; }
    public decimal? MinTemperatureC { get; init; }
    public decimal? MaxTemperatureC { get; init; }
    public string? HandlingNotes { get; init; }
}
