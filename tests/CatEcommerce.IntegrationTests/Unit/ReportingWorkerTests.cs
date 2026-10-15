using CatEcommerce.Api.Background;
using CatEcommerce.Infrastructure.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>HO-RPT-001 section 12.2 -- U-X2: a pass that throws is logged and the next pass still runs; <c>ExecuteAsync</c> never throws. No host, no database.</summary>
public sealed class ReportingWorkerTests
{
    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public int Passes;

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref Passes);
            return new Scope();
        }

        // GetService returns null, so GetRequiredService<ReportingWorkCycle> throws InvalidOperationException: a pass that fails.
        private sealed class Scope : IServiceScope, IServiceProvider
        {
            public IServiceProvider ServiceProvider => this;

            public object? GetService(Type serviceType) => null;

            public void Dispose() { }
        }
    }

    private sealed class RecordingLogger : ILogger<ReportingWorker>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Levels) Levels.Add(logLevel);
        }
    }

    [Fact]
    public async Task U_X2_APassThatThrows_IsLogged_AndTheNextPassStillRuns_ExecuteAsyncNeverThrows()
    {
        var scopes = new ThrowingScopeFactory();
        var logger = new RecordingLogger();
        var options = Options.Create(new ReportingOptions { Worker = new ReportingWorkerOptions { Enabled = true, IntervalSeconds = 5 } });
        var worker = new ReportingWorker(scopes, options, logger);

        await worker.StartAsync(CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (Volatile.Read(ref scopes.Passes) < 2 && DateTime.UtcNow < deadline) await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(scopes.Passes >= 2, $"Only {scopes.Passes} pass(es) ran.");
        Assert.False(worker.ExecuteTask?.IsFaulted ?? false);
        lock (logger.Levels) Assert.True(logger.Levels.Count(l => l == LogLevel.Error) >= 2);
    }

    [Fact]
    public async Task ADisabledWorker_RunsNoPass()
    {
        var scopes = new ThrowingScopeFactory();
        var options = Options.Create(new ReportingOptions { Worker = new ReportingWorkerOptions { Enabled = false } });
        var worker = new ReportingWorker(scopes, options, new RecordingLogger());

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, scopes.Passes);
    }
}
