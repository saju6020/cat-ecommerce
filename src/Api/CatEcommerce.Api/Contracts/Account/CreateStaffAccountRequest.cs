namespace CatEcommerce.Api.Contracts.Account;

public sealed class CreateStaffAccountRequest
{
    public required string FullName { get; init; }
    public string? MobileNumber { get; init; }
    public string? EmailAddress { get; init; }
    public required IReadOnlyList<string> RoleIds { get; init; }
    /// <summary>Owner, 2026-10-19: optional starting password the administrator shares with the staff member.</summary>
    public string? Password { get; init; }
}
