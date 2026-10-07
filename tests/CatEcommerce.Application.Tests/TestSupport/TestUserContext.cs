using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.TestSupport;

internal static class TestUserContext
{
    public static UserContext Create() => new(Guid.NewGuid(), Guid.NewGuid());
}
