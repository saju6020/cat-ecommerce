using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Tests.TestSupport;

internal static class TestUserContext
{
    public static UserContext Create() => new(Guid.NewGuid(), Guid.NewGuid());
}
