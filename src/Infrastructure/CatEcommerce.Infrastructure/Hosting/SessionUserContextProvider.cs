using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Infrastructure.Hosting;

/// <summary>
/// HO-ACC-001 §12.5 / ADR-032. Replaces <see cref="DevUserContextProvider"/> as the source of
/// identity: staff -> <c>UserId = StaffAccountId</c>, <c>Roles</c> = role ids; customer ->
/// <c>UserId = CustomerAccountId</c>, <c>Roles = ["Customer"]</c>; none -> anonymous
/// (<see cref="Guid.Empty"/>, no roles). The Development fallback to <see cref="DevUserContextProvider"/>
/// applies <b>only</b> when no session cookie was presented at all.
/// </summary>
public sealed class SessionUserContextProvider(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    IHostEnvironment environment,
    DevUserContextProvider devUserContextProvider) : IUserContextProvider
{
    public UserContext GetUserContext()
    {
        var caller = httpContextAccessor.HttpContext?.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller
            ?? ResolvedCaller.Anonymous;

        var tenantId = ParseOrDefault(configuration["DevUser:TenantId"], DefaultTenantId);

        if (caller.Kind == ResolvedCallerKind.Anonymous)
        {
            if (IsDevUserEnabled() && environment.IsDevelopment())
                return devUserContextProvider.GetUserContext();

            return new UserContext(Guid.Empty, tenantId) { UserName = "anonymous" };
        }

        return new UserContext(caller.AccountId, tenantId)
        {
            UserName = caller.Kind == ResolvedCallerKind.Staff ? "staff" : "customer",
            Roles = caller.RoleIds.ToArray()
        };
    }

    private bool IsDevUserEnabled() => bool.TryParse(configuration["DevUser:Enabled"], out var enabled) && enabled;

    private static Guid ParseOrDefault(string? value, Guid fallback) =>
        Guid.TryParse(value, out var parsed) ? parsed : fallback;

    private static readonly Guid DefaultTenantId = new("00000000-0000-0000-0000-000000000002");
}
