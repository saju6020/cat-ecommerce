using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

public sealed class GuaranteedAnalysisInput
{
    public required decimal ProteinPercent { get; init; }
    public required decimal FatPercent { get; init; }
    public required decimal FibrePercent { get; init; }
    public required decimal MoisturePercent { get; init; }
}

/// <summary>HO-SHP-001 section 11.1 C8.</summary>
public sealed class SetFoodDetailsCommand : Command
{
    public required Guid ProductId { get; init; }
    public required int ExpectedVersion { get; init; }
    public string? Manufacturer { get; init; }
    public string? LifeStage { get; init; }
    public string? ProteinSource { get; init; }
    public string? IngredientList { get; init; }
    public GuaranteedAnalysisInput? GuaranteedAnalysis { get; init; }
    public string? FeedingGuide { get; init; }
    public string? StorageInstructions { get; init; }
}
