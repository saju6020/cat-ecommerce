using CatEcommerce.Application.CommandHandlers.Boarding;
using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Domain.DomainServices;
using Platform.Infrastructure.Core.Commands;
using Xunit;

namespace CatEcommerce.Application.Tests.CommandHandlers.Boarding;

public sealed class ClaimRetryTests
{
    private sealed class Handler : ICommandHandlerAsync<ReserveBoardingStayCommand>
    {
        public Task<CommandResponse> HandleAsync(ReserveBoardingStayCommand command) => Task.FromResult(new CommandResponse { IsSuccess = true });
    }

    private sealed class FakeScopes(Handler fresh) : IFreshScopeExecutor
    {
        public int Discards { get; private set; }

        public Task<TResult> ExecuteAsync<TService, TResult>(Func<TService, Task<TResult>> work) where TService : notnull => work((TService)(object)fresh);

        public void DiscardRequestScopeChanges() => Discards++;
    }

    private static Task<CommandResponse> RunAsync(FakeScopes scopes, Handler first, Func<Handler, ReserveBoardingStayCommand, Task<CommandResponse>> attempt, Func<CommandResponse>? exhausted = null) =>
        ClaimRetry.RunAsync(first, new ReserveBoardingStayCommand { StayId = Guid.NewGuid() }, attempt, scopes, e => e is InvalidOperationException, exhausted);

    [Fact]
    public async Task ALostFirstAttempt_ClearsTheRequestScope_AndTheRetryInTheFreshScopeSucceeds()
    {
        var fresh = new Handler();
        var scopes = new FakeScopes(fresh);
        var first = new Handler();

        var response = await RunAsync(scopes, first, (h, _) => ReferenceEquals(h, first) ? throw new InvalidOperationException("lost") : Task.FromResult(new CommandResponse { IsSuccess = true }));

        Assert.True(response.IsSuccess);
        Assert.Equal(1, scopes.Discards);
    }

    [Fact]
    public async Task WhenEveryAttemptIsLost_TheHonestRefusalIsReturned_AfterFourAttempts()
    {
        var scopes = new FakeScopes(new Handler());
        var attempts = 0;

        var response = await RunAsync(scopes, new Handler(), (_, _) => { attempts++; throw new InvalidOperationException("lost"); }, () => new CommandResponse { IsSuccess = false });

        Assert.False(response.IsSuccess);
        Assert.Equal(ClaimRetry.MaxAttempts, attempts);
        Assert.Equal(1, scopes.Discards);
    }

    [Fact]
    public async Task AFirstAttemptThatSucceeds_TouchesNothing()
    {
        var scopes = new FakeScopes(new Handler());

        var response = await RunAsync(scopes, new Handler(), (_, _) => Task.FromResult(new CommandResponse { IsSuccess = true }));

        Assert.True(response.IsSuccess);
        Assert.Equal(0, scopes.Discards);
    }
}
