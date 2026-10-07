using CatEcommerce.Domain.DomainServices;

namespace CatEcommerce.Application.Tests.TestSupport;

public sealed class FakeSessionPolicy : ISessionPolicy
{
    public TimeSpan CustomerSessionLifetime => TimeSpan.FromDays(7);
    public TimeSpan StaffSessionLifetime => TimeSpan.FromHours(12);
    public TimeSpan StaffIdleTimeout => TimeSpan.FromMinutes(30);
}
