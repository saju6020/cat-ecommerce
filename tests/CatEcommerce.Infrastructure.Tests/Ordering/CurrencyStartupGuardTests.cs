using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Ordering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatEcommerce.Infrastructure.Tests.Ordering;

/// <summary>HO-ORD-001 U-A4 -- a currency with other than two minor digits is refused at start-up (OQ-5).</summary>
public sealed class CurrencyStartupGuardTests
{
    private sealed class Settings(string currency) : IBusinessSettings
    {
        public int GetInt(string key, Guid? branchId = null) => 0;
        public decimal GetDecimal(string key, Guid? branchId = null) => 0;
        public bool GetBool(string key, Guid? branchId = null) => false;
        public TimeRange GetTimeRange(string key, Guid? branchId = null) => throw new NotSupportedException();
        public string GetString(string key, Guid? branchId = null) => currency;
    }

    private sealed class ThrowingSettings : IBusinessSettings
    {
        public int GetInt(string key, Guid? branchId = null) => throw new InvalidOperationException("database down");
        public decimal GetDecimal(string key, Guid? branchId = null) => throw new InvalidOperationException("database down");
        public bool GetBool(string key, Guid? branchId = null) => throw new InvalidOperationException("database down");
        public TimeRange GetTimeRange(string key, Guid? branchId = null) => throw new InvalidOperationException("database down");
        public string GetString(string key, Guid? branchId = null) => throw new InvalidOperationException("database down");
    }

    private static CurrencyStartupGuard GuardFor(IBusinessSettings settings)
    {
        var services = new ServiceCollection().AddSingleton(settings).BuildServiceProvider();
        return new CurrencyStartupGuard(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<CurrencyStartupGuard>.Instance);
    }

    [Fact]
    public async Task U_A4_ABdtConfiguration_StartsUp() => await GuardFor(new Settings("BDT")).StartAsync(default);

    [Theory]
    [InlineData("KWD")]
    [InlineData("JPY")]
    [InlineData("XXX")]
    public async Task U_A4_AThreeOrZeroDigitCurrency_RefusesStartUp(string currency)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => GuardFor(new Settings(currency)).StartAsync(default));
        Assert.Contains(currency, ex.Message);
    }

    [Fact]
    public async Task AnUnreadableSetting_IsLogged_NotAStartUpRefusal() => await GuardFor(new ThrowingSettings()).StartAsync(default);
}
