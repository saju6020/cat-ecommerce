namespace CatEcommerce.Read.ViewModels;

public sealed record DeliveryAddressViewModel(
    Guid Id, string Label, string Line1, string? Line2, string City, string District,
    string? PostalCode, string? DeliveryInstructions, bool IsDefault);

public sealed record DeliveryAddressBookViewModel(IReadOnlyList<DeliveryAddressViewModel> Addresses, int MaxAddresses);
