using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Application.Tests.TestSupport;

/// <summary>HO-POR-001 §15 -- upstream handlers are hand-written fakes; no database. Records every query it receives, in order.</summary>
internal sealed class FakeQueryHandler<TQuery, TResult>(Func<TQuery, TResult> respond) : IQueryHandlerAsync<TQuery, TResult>
    where TQuery : Query<TResult>
{
    public List<TQuery> Received { get; } = [];

    public Task<QueryResponse<TResult>> HandleAsync(TQuery query)
    {
        Received.Add(query);
        return Task.FromResult(new QueryResponse<TResult> { Result = respond(query) });
    }
}

/// <summary>Records overlapping calls: <see cref="Overlapped"/> flips if a second call starts before the first finishes (ADR-267, F36).</summary>
internal sealed class OverlapDetector
{
    private int active;

    public bool Overlapped { get; private set; }

    public async Task<T> RunAsync<T>(T value)
    {
        if (Interlocked.Increment(ref active) > 1) Overlapped = true;
        await Task.Delay(20);
        Interlocked.Decrement(ref active);
        return value;
    }
}

/// <summary>Captures log entries so a test can assert the level, the message and what the line does NOT contain.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception), exception));
}

internal static class PortalTestCallers
{
    public static UserContext For(Guid customerId) => new(customerId, Guid.NewGuid());
}
