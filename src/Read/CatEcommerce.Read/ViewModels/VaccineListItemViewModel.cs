namespace CatEcommerce.Read.ViewModels;

/// <summary>GET /api/vaccines, Q6.</summary>
public sealed record VaccineListItemViewModel(
    Guid Id, string Code, string Name, bool IsCore, int ValidityMonths, bool IsActive);
