namespace CatEcommerce.Api.Contracts.Account;

/// <summary>Owner, 2026-10-19: the new password an administrator sets for a staff member.</summary>
public sealed class SetStaffPasswordRequest
{
    public required string Password { get; init; }
}
