namespace CatEcommerce.Read.ViewModels;

public sealed record PersonalDataExportCatViewModel(Guid Id, string Name, string Species, string? Breed, string Sex);

public sealed record PersonalDataExportViewModel(
    int SchemaVersion,
    DateTimeOffset ExportedOn,
    CustomerProfileViewModel Profile,
    IReadOnlyList<DeliveryAddressViewModel> Addresses,
    IReadOnlyList<NotificationPreferenceCellViewModel> NotificationPreferences,
    IReadOnlyList<PersonalDataExportCatViewModel> Cats);
