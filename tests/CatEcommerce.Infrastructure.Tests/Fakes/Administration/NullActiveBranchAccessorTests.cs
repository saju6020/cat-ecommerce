using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Fakes.Administration;

namespace CatEcommerce.Infrastructure.Tests.Fakes.Administration;

public sealed class NullActiveBranchAccessorTests
{
    [Fact]
    public void GetActiveBranchId_is_always_NoBranch()
    {
        var sut = new NullActiveBranchAccessor();

        Assert.Equal(ActiveBranchResult.NoBranch, sut.GetActiveBranchId());
    }
}
