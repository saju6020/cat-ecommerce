namespace CatEcommerce.Api.Contracts.Account;

public sealed class ChangeStaffRolesRequest
{
    public required IReadOnlyList<string> RoleIds { get; init; }
}
