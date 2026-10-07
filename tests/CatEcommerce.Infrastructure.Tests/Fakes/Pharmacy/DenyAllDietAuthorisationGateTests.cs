using CatEcommerce.Infrastructure.Fakes.Pharmacy;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Tests.Fakes.Pharmacy;

/// <summary>
/// DenyAllDietAuthorisationGate must fail closed (README C-4, ADR-084): a prescription diet product
/// is unpurchasable, not accidentally open, until HO-PHM-001 lands.
/// </summary>
public sealed class DenyAllDietAuthorisationGateTests
{
    private readonly DenyAllDietAuthorisationGate _sut = new();

    [Fact]
    public async Task CheckAsync_always_denies()
    {
        var result = await _sut.CheckAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(DietGateOutcome.NoAuthorisation, result.Outcome);
        Assert.Null(result.AuthorisationId);
        Assert.Null(result.ValidTo);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public async Task GetAuthorisedCatsAsync_always_returns_no_cats()
    {
        var cats = await _sut.GetAuthorisedCatsAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(cats);
    }
}
