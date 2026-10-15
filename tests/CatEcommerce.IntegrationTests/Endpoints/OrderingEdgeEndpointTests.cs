using System.Net;
using System.Net.Http.Json;
using System.Text;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.IntegrationTests.Ordering;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 Phase A (US-ORD-18, ADR-202): the HTTP idempotency edge, driven by real requests against a probe controller that carries
/// <c>[Idempotent]</c>; the <c>ops</c> schema; and the Npgsql facts the rest of the slice leans on. Covers U-X2, I-A1 (the <c>ops</c> half), I-A2
/// and the UTC-normalisation rule.
/// </summary>
public sealed class OrderingEdgeEndpointTests(OrderingApiFactory factory) : OrderingIntegrationTestBase(factory)
{
    private static string Url(string name) => $"/api/test-probe/idempotency/{name}";
    private static string NewName() => "probe-" + Guid.NewGuid().ToString("N");

    private static HttpRequestMessage Post(string name, string json, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Url(name)) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    // ── U-X2 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task U_X2_SameKeySameBody_RunsTheActionOnce_AndReplaysTheFirstResponse()
    {
        var client = CreateClient();
        var name = NewName();
        var key = Guid.NewGuid().ToString();

        var first = await ExpectAsync(HttpStatusCode.Created, await client.SendAsync(Post(name, """{"value":"a"}""", key)));
        var second = await ExpectAsync(HttpStatusCode.Created, await client.SendAsync(Post(name, """{"value":"a"}""", key)));

        Assert.False(first.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal("true", Assert.Single(second.Headers.GetValues("Idempotent-Replayed")));
        Assert.Equal((await ReadAsync(first)).GetProperty("id").GetGuid(), (await ReadAsync(second)).GetProperty("id").GetGuid());
        Assert.Equal(1, IdempotencyProbeController.Executions[name]);
    }

    [Fact]
    public async Task U_X2_ABodyThatDiffersOnlyInKeyOrderOrSpacing_IsTheSameRequest()
    {
        var client = CreateClient();
        var name = NewName();
        var key = Guid.NewGuid().ToString();

        await ExpectAsync(HttpStatusCode.Created, await client.SendAsync(Post(name, """{"value":"a","status":null,"fail":false}""", key)));
        var replay = await ExpectAsync(HttpStatusCode.Created, await client.SendAsync(Post(name, """ { "fail" : false, "status" : null, "value" : "a" } """, key)));

        Assert.True(replay.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(1, IdempotencyProbeController.Executions[name]);
    }

    [Fact]
    public async Task U_X2_SameKeyDifferentBody_Is422IdempotencyKeyReused_AndTheActionDoesNotRunAgain()
    {
        var client = CreateClient();
        var name = NewName();
        var key = Guid.NewGuid().ToString();

        await ExpectAsync(HttpStatusCode.Created, await client.SendAsync(Post(name, """{"value":"a"}""", key)));
        var reused = await client.SendAsync(Post(name, """{"value":"b"}""", key));

        await AssertErrorAsync(reused, "IdempotencyKeyReused", HttpStatusCode.UnprocessableEntity);
        Assert.Equal(1, IdempotencyProbeController.Executions[name]);
    }

    [Fact]
    public async Task U_X2_SameKeyOnAnotherPath_IsADifferentRequest()
    {
        var client = CreateClient();
        var key = Guid.NewGuid().ToString();

        await ExpectAsync(HttpStatusCode.Created, await client.SendAsync(Post(NewName(), """{"value":"a"}""", key)));
        var other = await client.SendAsync(Post(NewName(), """{"value":"a"}""", key));

        await AssertErrorAsync(other, "IdempotencyKeyReused", HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task U_X2_ASecondRequestWhileTheFirstIsRunning_Is409RequestInProgress_ThenTheFirstCompletesAndReplays()
    {
        var client = CreateClient();
        var name = NewName();
        var key = Guid.NewGuid().ToString();
        var gate = IdempotencyProbeController.Gates.GetOrAdd(name, _ => new SemaphoreSlim(0));

        var firstTask = client.SendAsync(Post(name, """{"value":"slow","waitForGate":true}""", key));
        await WaitUntilAsync(() => IdempotencyProbeController.Executions.GetValueOrDefault(name) == 1);

        var concurrent = await client.SendAsync(Post(name, """{"value":"slow","waitForGate":true}""", key));
        await AssertErrorAsync(concurrent, "RequestInProgress", HttpStatusCode.Conflict);

        gate.Release();
        await ExpectAsync(HttpStatusCode.Created, await firstTask);

        var replay = await ExpectAsync(HttpStatusCode.Created, await client.SendAsync(Post(name, """{"value":"slow","waitForGate":true}""", key)));
        Assert.True(replay.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(1, IdempotencyProbeController.Executions[name]);
    }

    [Fact]
    public async Task U_X2_TwentyConcurrentSubmissionsOfOneKey_RunTheActionExactlyOnce()
    {
        var name = NewName();
        var key = Guid.NewGuid().ToString();
        var gate = IdempotencyProbeController.Gates.GetOrAdd(name, _ => new SemaphoreSlim(0));
        var clients = Enumerable.Range(0, 20).Select(_ => CreateClient()).ToList();

        var tasks = clients.Select(c => c.SendAsync(Post(name, """{"value":"x","waitForGate":true}""", key))).ToList();
        await WaitUntilAsync(() => IdempotencyProbeController.Executions.GetValueOrDefault(name) >= 1);
        await Task.Delay(300); // let every loser hit the claim while the winner is parked at the gate
        gate.Release(20);

        var responses = await Task.WhenAll(tasks);
        Assert.Equal(1, IdempotencyProbeController.Executions[name]);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created && !r.Headers.Contains("Idempotent-Replayed"));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task U_X2_AMissingOrMalformedKey_Is400IdempotencyKeyRequired(string? key)
    {
        var name = NewName();
        var response = await CreateClient().SendAsync(Post(name, """{"value":"a"}""", key));

        await AssertErrorAsync(response, "IdempotencyKeyRequired");
        Assert.False(IdempotencyProbeController.Executions.ContainsKey(name));
    }

    [Fact]
    public async Task U_X2_AnUnhandledException_FreesTheKey_SoARetryRuns()
    {
        var client = CreateClient();
        var name = NewName();
        var key = Guid.NewGuid().ToString();

        var failed = await client.SendAsync(Post(name, """{"value":"a","fail":true}""", key));
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);

        // The retry is the same key but a different body (the failing flag is a body field): a freed key is claimable by any body.
        var retry = await ExpectAsync(HttpStatusCode.Created, await client.SendAsync(Post(name, """{"value":"a"}""", key)));
        Assert.False(retry.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(2, IdempotencyProbeController.Executions[name]);
    }

    [Fact]
    public async Task U_X2_A4xxResultIsStoredAndReplayed()
    {
        var client = CreateClient();
        var name = NewName();
        var key = Guid.NewGuid().ToString();

        var first = await client.SendAsync(Post(name, """{"value":"a","status":409}""", key));
        var replay = await client.SendAsync(Post(name, """{"value":"a","status":409}""", key));

        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.True(replay.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(1, IdempotencyProbeController.Executions[name]);
    }

    [Fact]
    public async Task U_X2_A5xxResultIsNotStored_SoARetryRuns()
    {
        var client = CreateClient();
        var name = NewName();
        var key = Guid.NewGuid().ToString();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.SendAsync(Post(name, """{"value":"a","status":503}""", key))).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.SendAsync(Post(name, """{"value":"a","status":503}""", key))).StatusCode);
        Assert.Equal(2, IdempotencyProbeController.Executions[name]);
    }

    [Fact]
    public async Task TheClaimIsCommittedOnItsOwn_AndTheStoreNeverRidesTheRequestsContext()
    {
        var name = NewName();
        var key = Guid.NewGuid();
        await CreateClient().SendAsync(Post(name, """{"value":"a"}""", key.ToString()));

        var row = await WithDbAsync(db => db.IdempotencyRecords.AsNoTracking().Where(r => r.Key == key)
            .Select(r => new { r.State, r.ResponseStatus, r.Scope, r.RequestHash }).SingleAsync());

        Assert.Equal(IdempotencyState.Completed, row.State);
        Assert.Equal(201, row.ResponseStatus);
        Assert.StartsWith("Anonymous:", row.Scope);
        Assert.Equal(64, row.RequestHash.Length);
    }

    [Fact]
    public async Task APurge_RemovesOnlyRecordsOlderThanTheCutoff_InBatches()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

        var old = Guid.NewGuid();
        var recent = Guid.NewGuid();
        var scopeName = "purge-test:" + Guid.NewGuid().ToString("N");
        // Real time, not the shared adjustable clock: another class may have it days ahead, and a cutoff computed from it would delete the fresh records of tests running in parallel.
        var now = DateTimeOffset.UtcNow;
        await store.ClaimAsync(scopeName, old, new string('a', 64), now.AddHours(-30), TimeSpan.FromMinutes(10), default);
        await store.ClaimAsync(scopeName, recent, new string('b', 64), now.AddHours(-1), TimeSpan.FromMinutes(10), default);

        var purged = await store.PurgeAsync(now.AddHours(-IdempotencyRules24()), 1000, default);

        Assert.True(purged >= 1);
        var remaining = await WithDbAsync(db => db.IdempotencyRecords.AsNoTracking().Where(r => r.Scope == scopeName).Select(r => r.Key).ToListAsync());
        Assert.Equal([recent], remaining);
    }

    private static int IdempotencyRules24() => CatEcommerce.Shared.Common.Constants.IdempotencyRules.RetentionHours;

    // ── I-A1 (ops half) ─────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_A1_TheOpsSchemaHoldsBothEdgeTables_WithTheirUniqueIndexes()
    {
        await using var connection = await OpenConnectionAsync();

        var tables = await ScalarListAsync(connection, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'ops' ORDER BY table_name");
        Assert.Contains("idempotency_record", tables);
        Assert.Contains("inbound_webhook_receipt", tables);

        var indexes = await ScalarListAsync(connection, "SELECT indexname FROM pg_indexes WHERE schemaname = 'ops'");
        Assert.Contains("ix_idempotency_record_scope_key", indexes);
        Assert.Contains("ix_idempotency_record_created_on", indexes);
        Assert.Contains("ix_inbound_webhook_receipt_provider_event", indexes);

        var unique = await ScalarListAsync(connection,
            "SELECT i.relname FROM pg_index x JOIN pg_class i ON i.oid = x.indexrelid JOIN pg_namespace n ON n.oid = i.relnamespace WHERE n.nspname = 'ops' AND x.indisunique");
        Assert.Contains("ix_idempotency_record_scope_key", unique);
        Assert.Contains("ix_inbound_webhook_receipt_provider_event", unique);
    }

    [Fact]
    public async Task TheWebhookInbox_RefusesAnEventIdItAlreadyHolds()
    {
        var provider = "p-" + Guid.NewGuid().ToString("N")[..8];
        var eventId = "evt-" + Guid.NewGuid().ToString("N");

        async Task InsertAsync()
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
            db.InboundWebhookReceipts.Add(new InboundWebhookReceipt { Id = Guid.NewGuid(), Provider = provider, EventId = eventId, ReceivedOn = Now });
            await db.SaveChangesAsync();
        }

        await InsertAsync();
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(InsertAsync);
        Assert.Equal("ix_inbound_webhook_receipt_provider_event", Assert.IsType<PostgresException>(duplicate.InnerException).ConstraintName);
    }

    // ── I-A2 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_A2_TheHostStartsWithScopeValidationOn_AndAnswersHealth()
    {
        // WebApplicationFactory builds the host with ValidateScopes and ValidateOnBuild (Program.cs) -- a registration that cannot resolve would have
        // failed here already. The ordering ports resolve from a request scope:
        await using var scope = Factory.Services.CreateAsyncScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIdempotencyStore>());

        var health = await CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    // ── The UTC rule ────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task AClientInstantWithAnOffset_IsRefusedByNpgsql_UntilItIsNormalisedToUtc()
    {
        // 2026-10-09T10:00:00+06:00 is 04:00 UTC. Npgsql will not write a non-zero offset to timestamptz, so a command mapper must normalise.
        var clientInstant = DateTimeOffset.Parse("2026-10-09T10:00:00+06:00");

        async Task<Exception?> SaveAsync(DateTimeOffset receivedOn)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
            db.InboundWebhookReceipts.Add(new InboundWebhookReceipt { Id = Guid.NewGuid(), Provider = "utc-test", EventId = Guid.NewGuid().ToString("N"), ReceivedOn = receivedOn });
            try { await db.SaveChangesAsync(); return null; }
            catch (Exception ex) { return ex; }
        }

        var raw = await SaveAsync(clientInstant);
        Assert.NotNull(raw);
        Assert.Contains("offset", (raw!.InnerException ?? raw).Message, StringComparison.OrdinalIgnoreCase);

        Assert.Null(await SaveAsync(clientInstant.ToUtc()));
        Assert.Equal(TimeSpan.Zero, clientInstant.ToUtc().Offset);
        Assert.Equal(clientInstant.UtcDateTime, clientInstant.ToUtc().UtcDateTime);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("The condition was not met in time.");
            await Task.Delay(20);
        }
    }

    private static async Task<List<string>> ScalarListAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync()) values.Add(reader.GetString(0));
        return values;
    }
}
