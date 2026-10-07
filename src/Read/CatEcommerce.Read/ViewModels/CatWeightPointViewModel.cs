namespace CatEcommerce.Read.ViewModels;

/// <summary>GET /api/cats/{catId}/weights, Q3 -- US-CAT-02 AC2's chart.</summary>
public sealed record CatWeightPointViewModel(DateOnly RecordedOn, decimal Kilograms);
