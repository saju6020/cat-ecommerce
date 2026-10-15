using CatEcommerce.Application.CommandHandlers.Order;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Order;

/// <summary>
/// HO-ORD-001-FU1 D1/D2 -- who the counter sale's buyer is, and when the buyer is stored. The handler is driven with in-memory doubles up to the first business refusal
/// (nothing to sell); every dependency past that point is never touched, so it is left null. The HTTP behaviour is proved by the integration tests.
/// </summary>
public sealed class CompleteCounterSaleBuyerTests
{
    private static readonly UserContext Cashier = new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Reception"] };
    private static readonly Guid BranchId = Guid.NewGuid();

    private sealed class Finder(CustomerContactFacts? found) : ICounterCustomerFinder
    {
        public Task<CustomerContactFacts?> FindActiveByPrimaryMobileAsync(string normalisedMobile, CancellationToken ct) => Task.FromResult(found);
    }

    private sealed class Registrar(CounterBuyerResolution answer) : ICounterBuyerRegistrar
    {
        public List<(string Mobile, string Name)> Calls { get; } = [];

        public Task<CounterBuyerResolution> FindOrRegisterAsync(string normalisedMobile, string fullName, UserContext userContext, Guid correlationId, CancellationToken ct)
        {
            Calls.Add((normalisedMobile, fullName));
            return Task.FromResult(answer);
        }
    }

    /// <summary>Inputs with no sellable line, so the pricing answers "nothing to place" -- a business refusal that comes after the buyer is resolved.</summary>
    private sealed class EmptyInputsLoader : ICheckoutInputsLoader
    {
        public Guid? CustomerIdSeen { get; private set; }

        public Task<CheckoutInputs> LoadAsync(Guid customerId, FulfilmentChoice choice, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();

        public Task<CheckoutInputs> LoadCounterAsync(Guid branchId, Guid? customerId, IReadOnlyList<CounterLineRequest> lines, string? discountCode, DateTimeOffset now, CancellationToken ct)
        {
            CustomerIdSeen = customerId;
            var choice = new FulfilmentChoice(FulfilmentMethod.Counter, null, null, null, null, null, OrderPaymentMethod.InStore, null);
            return Task.FromResult(new CheckoutInputs(
                now, DateOnly.FromDateTime(now.Date), "BDT", [], choice, null, null, new HashSet<DateOnly>(), [], null, null, new CustomerDiscountFacts(0, false),
                new PaymentSettingsFacts(false, 0m, false, false, false), [], branchId));
        }
    }

    private static FakeAggregateRootRepository<BranchAggregate> Branches()
    {
        var branch = new BranchAggregate(BranchId);
        branch.Create(new BranchCreateDto("MAIN", "Main", "Asia/Dhaka", null, null, null, Cashier, Guid.NewGuid()));
        var repository = new FakeAggregateRootRepository<BranchAggregate>();
        repository.Seed(branch);
        return repository;
    }

    private static CompleteCounterSaleCommand Sale(string mobile = "01712345678") => new()
    {
        OrderId = Guid.NewGuid(), IdempotencyKey = Guid.NewGuid(), BranchId = BranchId, CustomerName = "Rina Akter", CustomerMobile = mobile,
        Lines = [new CounterSaleLineInput(Guid.NewGuid(), 1, null)], Tenders = [new CounterTenderInput(TenderMethod.Cash, 100m, null)],
        UserContext = Cashier, CorrelationId = Guid.NewGuid(),
    };

    private static CompleteCounterSaleCommandHandler Handler(ICounterCustomerFinder finder, ICounterBuyerRegistrar registrar, ICheckoutInputsLoader loader) =>
        new(null!, null!, Branches(), loader, finder, registrar, null!, null!, null!, null!, null!, null!, new FakeTimeProvider(new DateTimeOffset(2026, 10, 10, 8, 0, 0, TimeSpan.Zero)));

    private static string[] Codes(Platform.Infrastructure.Core.Commands.CommandResponse response) => response.ValidationResult?.Errors.Select(e => e.ErrorCode).ToArray() ?? [];

    // D2: the buyer is stored for a validated request even though the sale is then refused for a business reason.
    [Fact]
    public async Task ARefusedSale_StillFindsOrRegistersTheBuyer_BeforeTheBusinessChecks()
    {
        var registered = new CustomerContactFacts(Guid.NewGuid(), "Rina Akter", "+8801712345678", IsActive: true);
        var registrar = new Registrar(CounterBuyerResolution.Found(registered));
        var loader = new EmptyInputsLoader();

        var response = await Handler(new Finder(null), registrar, loader).HandleAsync(Sale());

        Assert.False(response.IsSuccess);                                    // the sale is refused (nothing to sell) ...
        var call = Assert.Single(registrar.Calls);                           // ... and the walk-in was registered first
        Assert.Equal("Rina Akter", call.Name);
        Assert.Equal(registered.CustomerId, loader.CustomerIdSeen);          // the checks run against the registered buyer
    }

    [Fact]
    public async Task AnExistingActiveBuyer_IsNotRegisteredAgain()
    {
        var existing = new CustomerContactFacts(Guid.NewGuid(), "Rina Akter", "+8801712345678", IsActive: true);
        var registrar = new Registrar(CounterBuyerResolution.Refused("X", "x", "x"));

        await Handler(new Finder(existing), registrar, new EmptyInputsLoader()).HandleAsync(Sale());

        Assert.Empty(registrar.Calls);
    }

    [Fact]
    public async Task AMobileThatIsNotAValidNumber_RegistersNobody()
    {
        var registrar = new Registrar(CounterBuyerResolution.Refused("X", "x", "x"));

        var response = await Handler(new Finder(null), registrar, new EmptyInputsLoader()).HandleAsync(Sale(mobile: "12345"));

        Assert.False(response.IsSuccess);
        Assert.Empty(registrar.Calls);
    }

    // D1: a mobile whose account cannot be sold to (no Active survivor) is the cashier-facing refusal, before any check on the goods.
    [Fact]
    public async Task AMobileWhoseAccountIsNotActive_AndHasNoActiveSurvivor_IsTheCashierFacingRefusal()
    {
        var registrar = new Registrar(CounterBuyerResolution.Refused(BusinessViolationCodes.AccountNotActive, OrderMessages.CounterMobileHeldByInactiveAccount, "CustomerMobile"));
        var loader = new EmptyInputsLoader();

        var response = await Handler(new Finder(null), registrar, loader).HandleAsync(Sale());

        Assert.Equal([BusinessViolationCodes.AccountNotActive], Codes(response));
        Assert.Null(loader.CustomerIdSeen);                                  // nothing was priced
    }
}
