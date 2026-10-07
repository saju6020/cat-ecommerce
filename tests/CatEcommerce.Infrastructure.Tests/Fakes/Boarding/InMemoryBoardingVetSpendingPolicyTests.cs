using CatEcommerce.Infrastructure.Fakes.Boarding;

namespace CatEcommerce.Infrastructure.Tests.Fakes.Boarding;

/// <summary>
/// InMemoryBoardingVetSpendingPolicy must fail closed: zero headroom, so a consumer always falls
/// back to the approval workflow rather than allowing an unbounded clinical charge.
/// </summary>
public sealed class InMemoryBoardingVetSpendingPolicyTests
{
    [Fact]
    public async Task GetHeadroomAsync_reports_zero_headroom_and_no_authorisation()
    {
        var sut = new InMemoryBoardingVetSpendingPolicy();

        var result = await sut.GetHeadroomAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.VetVisitAuthorised);
        Assert.Equal(0m, result.Limit.Amount);
        Assert.Equal(0m, result.Committed.Amount);
        Assert.Equal(0m, result.Headroom.Amount);
    }
}
