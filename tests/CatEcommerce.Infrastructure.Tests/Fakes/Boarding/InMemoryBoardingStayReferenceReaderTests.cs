using CatEcommerce.Infrastructure.Fakes.Boarding;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Tests.Fakes.Boarding;

/// <summary>
/// InMemoryBoardingStayReferenceReader must fail closed: no stay or deposit reference can be
/// confirmed to exist before HO-BRD-001 lands.
/// </summary>
public sealed class InMemoryBoardingStayReferenceReaderTests
{
    private readonly InMemoryBoardingStayReferenceReader _sut = new();

    [Fact]
    public async Task ExistsForCustomer_always_false()
    {
        var exists = await _sut.ExistsForCustomer(
            BoardingPaymentReferenceKind.BoardingStayDeposit, Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(exists);
    }

    [Fact]
    public async Task IsInHouse_always_false()
    {
        var isInHouse = await _sut.IsInHouse(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(isInHouse);
    }
}
