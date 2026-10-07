namespace CatEcommerce.Read.ViewModels;

public sealed record StaffAccountSummaryViewModel(
    Guid Id, string FullName, string? MobileNumber, string? EmailAddress,
    IReadOnlyList<string> RoleIds, bool HasPassword);

public sealed record StaffAccountViewModel(
    Guid Id, string FullName, string? MobileNumber, string? EmailAddress,
    IReadOnlyList<string> RoleIds, bool HasPassword);
