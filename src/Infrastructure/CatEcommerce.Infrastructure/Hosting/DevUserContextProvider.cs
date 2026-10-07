using Microsoft.Extensions.Configuration;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Infrastructure.Hosting;

/// <summary>
/// The one seam the platform declares but does not fill (.ai/architecture.md section 6.1).
/// Fixed UserId/TenantId from configuration -- there is no sign-in in this slice (section 20.3).
/// No Microsoft.Extensions.Configuration.Binder reference in this slice (section 13), so the
/// configuration section is read via the plain indexer rather than GetValue&lt;T&gt;.
///
/// HO-ACC-001 §12.2 / ADR-032: this is now the Development-only <b>fallback</b> for
/// <c>IUserContextProvider</c>, used only when <see cref="Hosting.SessionUserContextProvider"/>
/// resolves no session cookie and <c>DevUser:Enabled</c> is <c>true</c> in Development. Also reads
/// <c>DevUser:Roles</c> (defaults to none) so a developer without a wired-in staff sign-in can
/// still reach a <c>[RequireAction]</c> endpoint locally.
/// </summary>
public sealed class DevUserContextProvider(IConfiguration configuration) : IUserContextProvider
{
    public UserContext GetUserContext()
    {
        var userId = ParseOrDefault(configuration["DevUser:UserId"], DefaultUserId);
        var tenantId = ParseOrDefault(configuration["DevUser:TenantId"], DefaultTenantId);
        var roles = configuration.GetSection("DevUser:Roles").GetChildren()
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .ToArray();

        return new UserContext(userId, tenantId)
        {
            UserName = "dev-user",
            DisplayName = "Development User",
            Roles = roles
        };
    }

    private static Guid ParseOrDefault(string? value, Guid fallback) =>
        Guid.TryParse(value, out var parsed) ? parsed : fallback;

    private static readonly Guid DefaultUserId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DefaultTenantId = new("00000000-0000-0000-0000-000000000002");
}
