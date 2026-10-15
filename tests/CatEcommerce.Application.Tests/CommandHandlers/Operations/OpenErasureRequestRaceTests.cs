using System.Linq.Expressions;
using CatEcommerce.Application.CommandHandlers.Operations;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.Tests.CommandHandlers.Operations;

/// <summary>HO-XCT-001 C1 (CODE_REVIEW 17): a concurrent open is "treat as exists, reload, retry once" -- the loser still succeeds and the ledger gains the missing scopes.</summary>
public sealed class OpenErasureRequestRaceTests
{
    private static readonly UserContext Worker = new(Guid.NewGuid(), Guid.Empty);
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    private static OpenErasureRequestCommand Command(Guid customerId) => new()
    {
        CustomerAccountId = customerId, RequestedOn = Now, Source = ErasureRequestSource.SelfService.DisplayName, UserContext = Worker, CorrelationId = Guid.NewGuid()
    };

    [Fact]
    public async Task ALostRace_ReloadsTheWinnersLedger_TopsItUp_AndSucceeds()
    {
        var customerId = Guid.NewGuid();
        var winner = new ErasureRequestAggregate(customerId);
        winner.Open(new ErasureOpenDto(Now, ErasureRequestSource.SelfService, ["account"], Worker, Guid.NewGuid()));
        var winnersRepository = new FakeAggregateRootRepository<ErasureRequestAggregate>();
        winnersRepository.Seed(winner);

        var services = new ServiceCollection()
            .AddSingleton<IAggregateRootRepository<ErasureRequestAggregate>>(winnersRepository)
            .AddSingleton<IPersonalDataEraser>(new FakeEraser("account")).AddSingleton<IPersonalDataEraser>(new FakeEraser("notification"))
            .BuildServiceProvider();

        var loser = new OpenErasureRequestCommandHandler(
            new SaveFailsRepository(), [new FakeEraser("account"), new FakeEraser("notification")],
            services.GetRequiredService<IServiceScopeFactory>(), NullLogger<OpenErasureRequestCommandHandler>.Instance);

        var response = await loser.HandleAsync(Command(customerId));

        Assert.True(response.IsSuccess);
        Assert.Equal(["account", "notification"], winner.Scopes.Select(s => s.ContextKey).Order().ToArray());
        Assert.Single(winnersRepository.Updated);
    }

    [Fact]
    public async Task ARealFailure_WhenNoLedgerExistsAfterwards_IsNotSwallowed()
    {
        var services = new ServiceCollection().AddSingleton<IAggregateRootRepository<ErasureRequestAggregate>>(new FakeAggregateRootRepository<ErasureRequestAggregate>()).BuildServiceProvider();
        var handler = new OpenErasureRequestCommandHandler(
            new SaveFailsRepository(new InvalidOperationException("duplicate key")), [new FakeEraser("account")],
            services.GetRequiredService<IServiceScopeFactory>(), NullLogger<OpenErasureRequestCommandHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(Command(Guid.NewGuid())));
    }

    [Fact]
    public async Task Cancellation_IsNeverTreatedAsALostRace_AndNoRetryIsAttempted()
    {
        var winnersRepository = new FakeAggregateRootRepository<ErasureRequestAggregate>();
        var customerId = Guid.NewGuid();
        var winner = new ErasureRequestAggregate(customerId);
        winner.Open(new ErasureOpenDto(Now, ErasureRequestSource.SelfService, ["account"], Worker, Guid.NewGuid()));
        winnersRepository.Seed(winner);                                               // a ledger WOULD be found if the retry ran
        var services = new ServiceCollection().AddSingleton<IAggregateRootRepository<ErasureRequestAggregate>>(winnersRepository).BuildServiceProvider();
        var handler = new OpenErasureRequestCommandHandler(
            new SaveFailsRepository(new OperationCanceledException()), [new FakeEraser("account"), new FakeEraser("notification")],
            services.GetRequiredService<IServiceScopeFactory>(), NullLogger<OpenErasureRequestCommandHandler>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.HandleAsync(Command(customerId)));
        Assert.Empty(winnersRepository.Updated);
    }

    /// <summary>Finds nothing, and its insert fails with the given exception (a primary-key violation, or a cancellation).</summary>
    private sealed class SaveFailsRepository(Exception failure) : IAggregateRootRepository<ErasureRequestAggregate>
    {
        public SaveFailsRepository() : this(new InvalidOperationException("duplicate key")) { }

        public Task<ErasureRequestAggregate> GetByIdAsync(Guid id) => Task.FromResult<ErasureRequestAggregate>(null!);
        public Task<ErasureRequestAggregate> GetByFilterAsync(Expression<Func<ErasureRequestAggregate, bool>> dataFilters) => Task.FromResult<ErasureRequestAggregate>(null!);
        public Task<bool> ExistAsync(Expression<Func<ErasureRequestAggregate, bool>> dataFilters) => Task.FromResult(false);
        public Task SaveAsync(ErasureRequestAggregate aggregate, EventPublishOption eventPublishOption) => throw failure;
        public Task UpdateAsync(ErasureRequestAggregate aggregate, EventPublishOption eventPublishOption) => Task.CompletedTask;
        public Task UpdateAsync(ErasureRequestAggregate aggregate, int expectedVersion, EventPublishOption eventPublishOption) => Task.CompletedTask;
    }
}
