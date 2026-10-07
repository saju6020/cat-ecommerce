namespace CatEcommerce.Api.Contracts.Account;

public sealed class CreateStaffAccountRequest
{
    public required string FullName { get; init; }
    public string? MobileNumber { get; init; }
    public string? EmailAddress { get; init; }
    public required IReadOnlyList<string> RoleIds { get; init; }
}
