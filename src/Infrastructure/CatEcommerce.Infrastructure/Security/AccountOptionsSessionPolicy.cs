using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Security;

/// <summary>Implements <see cref="ISessionPolicy"/> over <see cref="AccountOptions"/> (§12.2).</summary>
public sealed class AccountOptionsSessionPolicy(IOptions<AccountOptions> options) : ISessionPolicy
{
    public TimeSpan CustomerSessionLifetime => TimeSpan.FromHours(options.Value.CustomerSessionLifetimeHours);
    public TimeSpan StaffSessionLifetime => TimeSpan.FromHours(options.Value.StaffSessionLifetimeHours);
    public TimeSpan StaffIdleTimeout => TimeSpan.FromMinutes(options.Value.StaffIdleTimeoutMinutes);
}
