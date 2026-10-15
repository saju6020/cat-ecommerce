namespace CatEcommerce.Read.ViewModels;

// HO-PHM-001 §12.4 / §8.10 (US-PHM-08). Flat records; the status is the EFFECTIVE one -- an authorisation whose last day has passed reads Expired whether or not the sweep has run.

public sealed record DietAuthorisationViewModel(
    Guid Id, Guid CatId, Guid ProductId, string ProductName, DateOnly ValidFrom, DateOnly ValidTo, string Status, string AuthorisedBy, Guid VisitId, string? Notes);

public sealed record DietProductViewModel(Guid ProductId, string Name, string Brand);
