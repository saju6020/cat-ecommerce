namespace CatEcommerce.Read.ViewModels;

public sealed record CustomerProfileViewModel(
    string FullName,
    string? MobileNumber,
    string? EmailAddress,
    bool HasMobileNumber,
    bool HasEmailAddress,
    bool CanEditEmail,
    IReadOnlyList<string> AdditionalPhoneNumbers,
    DateTimeOffset RegisteredOn);
