namespace CatEcommerce.Read.ViewModels;

/// <summary>Feeds the QR endpoint, Q7 -- US-CAT-11.</summary>
public sealed record CatCageCardViewModel(
    Guid CatId, string Name, Guid OwnerId, bool IsHighRisk, string? HandlingNote, string ProfileUrl);
