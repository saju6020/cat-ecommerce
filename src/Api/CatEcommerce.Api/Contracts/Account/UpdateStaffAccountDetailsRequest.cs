namespace CatEcommerce.Api.Contracts.Account;

/// <summary>Owner, 2026-10-20: the name and sign-in identifiers an administrator sets for a staff member.</summary>
public sealed class UpdateStaffAccountDetailsRequest
{
    public required string FullName { get; init; }
    public string? MobileNumber { get; init; }
    public string? EmailAddress { get; init; }
}
