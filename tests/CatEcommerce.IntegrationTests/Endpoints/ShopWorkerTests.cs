using CatEcommerce.Api.Background;
using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-SHP-001 §15 U-X2 -- the worker loop survives a throwing pass: the failure is logged
/// and the next pass still runs (it never throws out of <c>ExecuteAsync</c>). Pure host-level test:
/// no database, no HTTP.</summary>
public sealed class ShopWorkerTests
{
    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public int ScopesCreated;

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref ScopesCreated);
            return new ThrowingScope();
        }

        private sealed class ThrowingScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new ThrowingProvider();
            public void Dispose() { }
        }

        private sealed class ThrowingProvider : IServiceProvider, IServiceProviderIsService
        {
            public object? GetService(Type serviceType) => throw new InvalidOperationException("A pass blew up.");
            public bool IsService(Type serviceType) => false;
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Entries) Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }

    [Fact]
    public async Task U_X2_AThrowingPass_IsLogged_AndTheNextPassStillRuns()
    {
        var scopes = new ThrowingScopeFactory();
        var logger = new CapturingLogger<ShopWorker>();
        var options = Options.Create(new ShopOptions { Worker = new ShopWorkerOptions { Enabled = true, IntervalSeconds = 1 } });
        var worker = new ShopWorker(scopes, options, logger);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(2600));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(scopes.ScopesCreated >= 2, $"Expected at least two passes, saw {scopes.ScopesCreated}.");

        List<(LogLevel Level, string Message, Exception? Exception)> errors;
        lock (logger.Entries) errors = logger.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        Assert.True(errors.Count >= 2, "Every failed pass is logged as an error.");
        Assert.All(errors, e => Assert.IsType<InvalidOperationException>(e.Exception));
    }

    [Fact]
    public async Task U_X2_ADisabledWorker_RunsNoPass()
    {
        var scopes = new ThrowingScopeFactory();
        var options = Options.Create(new ShopOptions { Worker = new ShopWorkerOptions { Enabled = false, IntervalSeconds = 1 } });
        var worker = new ShopWorker(scopes, options, new CapturingLogger<ShopWorker>());

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, scopes.ScopesCreated);
    }
}
