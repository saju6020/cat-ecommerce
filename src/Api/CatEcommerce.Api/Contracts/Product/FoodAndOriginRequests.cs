namespace CatEcommerce.Api.Contracts.Product;

public sealed class GuaranteedAnalysisRequest
{
    public required decimal ProteinPercent { get; init; }
    public required decimal FatPercent { get; init; }
    public required decimal FibrePercent { get; init; }
    public required decimal MoisturePercent { get; init; }
}

public sealed class SetFoodDetailsRequest
{
    public required int ExpectedVersion { get; init; }
    public string? Manufacturer { get; init; }
    public string? LifeStage { get; init; }
    public string? ProteinSource { get; init; }
    public string? IngredientList { get; init; }
    public GuaranteedAnalysisRequest? GuaranteedAnalysis { get; init; }
    public string? FeedingGuide { get; init; }
    public string? StorageInstructions { get; init; }
}

public sealed class SetPrescriptionRequiredRequest
{
    public required int ExpectedVersion { get; init; }
    public required bool PrescriptionRequired { get; init; }
}

public sealed class SourcingOriginRequest
{
    public string? SourcingNotes { get; init; }
    public bool IsFeatured { get; init; }
    public int SortOrder { get; init; }
}

public sealed class CreateSourcingOriginRequest
{
    public required string CountryCode { get; init; }
    public string? SourcingNotes { get; init; }
    public bool IsFeatured { get; init; }
    public int SortOrder { get; init; }
}
