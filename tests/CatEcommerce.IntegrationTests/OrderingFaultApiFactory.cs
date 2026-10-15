using System.Collections.Concurrent;
using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Ordering;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// The switches of <see cref="OrderingFaultApiFactory"/>: faults are keyed by customer (or one global flag for the purge pass) so a test arms exactly its own and
/// no other test is ever affected. Static, because the host the factory builds is in the same process.
/// </summary>
public static class OrderingFaults
{
    /// <summary>Customers whose <c>RecordPaymentCommand</c> throws -- the Billing payment stage of I-E7.</summary>
    public static ConcurrentDictionary<Guid, byte> FailPaymentsFor { get; } = new();

    /// <summary>Customers whose <c>RecordPaymentCommand</c> throws a <c>DbUpdateConcurrencyException</c> -- the benign conflict (two staff, or the worker) that must leave no reconciliation issue.</summary>
    public static ConcurrentDictionary<Guid, byte> ConflictPaymentsFor { get; } = new();

    /// <summary>Makes the idempotency purge throw (U-X1: a throwing pass is logged and the next one still runs).</summary>
    public static volatile bool PurgeThrows;
}

/// <summary>A factory whose host wraps Billing's <c>RecordPaymentCommand</c> handler and the idempotency store with the <see cref="OrderingFaults"/> switches.
/// Everything else is the real implementation.</summary>
public class OrderingFaultApiFactory : OrderingApiFactory
{
    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        base.ConfigureAdditionalServices(services);

        services.AddScoped<ICommandHandlerAsync<RecordPaymentCommand>>(sp => new FaultyRecordPaymentHandler(ActivatorUtilities.CreateInstance<RecordPaymentCommandHandler>(sp)));
        services.AddScoped<IIdempotencyStore>(sp => new FaultyIdempotencyStore(ActivatorUtilities.CreateInstance<EfIdempotencyStore>(sp)));
    }

    private sealed class FaultyRecordPaymentHandler(RecordPaymentCommandHandler inner) : ICommandHandlerAsync<RecordPaymentCommand>
    {
        public Task<CommandResponse> HandleAsync(RecordPaymentCommand command) =>
            OrderingFaults.ConflictPaymentsFor.ContainsKey(command.CustomerId)
                ? throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("injected concurrency conflict")
            : OrderingFaults.FailPaymentsFor.ContainsKey(command.CustomerId)
                ? throw new InvalidOperationException("injected payment failure")
                : inner.HandleAsync(command);
    }

    private sealed class FaultyIdempotencyStore(IIdempotencyStore inner) : IIdempotencyStore
    {
        public Task<IdempotencyClaim> ClaimAsync(string scope, Guid key, string requestHash, DateTimeOffset now, TimeSpan staleAfter, CancellationToken ct) =>
            inner.ClaimAsync(scope, key, requestHash, now, staleAfter, ct);

        public Task CompleteAsync(string scope, Guid key, IdempotentResponse response, DateTimeOffset now, CancellationToken ct) => inner.CompleteAsync(scope, key, response, now, ct);

        public Task ReleaseAsync(string scope, Guid key, CancellationToken ct) => inner.ReleaseAsync(scope, key, ct);

        public Task<int> PurgeAsync(DateTimeOffset olderThan, int batchSize, CancellationToken ct) =>
            OrderingFaults.PurgeThrows ? throw new InvalidOperationException("injected purge failure") : inner.PurgeAsync(olderThan, batchSize, ct);
    }
}

/// <summary>The test-only courier webhook provider's secret and header (see <see cref="OrderingApiFactory"/>), with the signing the provider would do.</summary>
public static class OrderingWebhook
{
    public const string Secret = "test-courier-webhook-secret-not-a-real-secret";
    public const string Header = "X-Signature";

    public static string Sign(string body) =>
        Convert.ToHexStringLower(System.Security.Cryptography.HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Secret), System.Text.Encoding.UTF8.GetBytes(body)));
}
