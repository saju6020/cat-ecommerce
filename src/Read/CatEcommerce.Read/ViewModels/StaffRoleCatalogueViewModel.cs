namespace CatEcommerce.Read.ViewModels;

public sealed record StaffCapabilityCellViewModel(string Capability, string RoleId, string Access, string? Qualifier);

public sealed record StaffRoleCatalogueViewModel(
    IReadOnlyList<string> RoleIds,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<StaffCapabilityCellViewModel> Cells);
