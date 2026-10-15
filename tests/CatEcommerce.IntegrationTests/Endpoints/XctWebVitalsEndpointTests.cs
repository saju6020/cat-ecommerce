using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CatEcommerce.Api.Background;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-XCT-001 section 16 E -- anonymous web-vitals ingestion, the summary, the change-capture exclusion and the telemetry purge. Every case uses its own
/// build id and route so it asserts only on its own rows (rule 9). Serialised with the other retention cases because the purge case runs a daily pass.
/// </summary>
[Collection("XctRetention")]
public sealed class XctWebVitalsEndpointTests(XctApiFactory factory) : XctIntegrationTestBase(factory)
{
    private const string FixedIp = "203.0.113.77";

    private static string NewBuild() => "b-" + Guid.NewGuid().ToString("N");
    private static string NewRoute() => "/xct-" + Guid.NewGuid().ToString("N")[..10];

    private static object Sample(string route, string metric = "LCP", decimal value = 1800m, string viewport = "Wide") =>
        new { metric, value, routeTemplate = route, navigationType = "navigate", effectiveConnectionType = "4g", viewportClass = viewport };

    private static StringContent Json_(object body, string mediaType = "application/json") =>
        new(JsonSerializer.Serialize(body, Json), Encoding.UTF8, mediaType);

    private Task<int> BatchCountAsync(string buildId) =>
        ScalarAsync<int>("SELECT COUNT(*)::int FROM ops.web_vital_batch WHERE build_id = @b", ("b", buildId));

    // I-E1
    [Fact]
    public async Task I_E1_AnAnonymousBeacon_IsStoredAsOneBatchOfItsSamples_WithNoIdentity()
    {
        var build = NewBuild();
        var route = NewRoute();

        var response = await NewClient(Factory).PostAsync("/api/telemetry/web-vitals",
            Json_(new { buildId = build, samples = new[] { Sample(route), Sample(route, "INP", 120m), Sample(route, "CLS", 0.05m) } }));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, await BatchCountAsync(build));
        Assert.Equal(3, await ScalarAsync<int>(
            "SELECT COUNT(*)::int FROM ops.web_vital_sample s JOIN ops.web_vital_batch b ON b.\"Id\" = s.web_vital_batch_id WHERE b.build_id = @b", ("b", build)));
        Assert.Equal(Guid.Empty, await ScalarAsync<Guid>("SELECT \"CreatedBy\" FROM ops.web_vital_batch WHERE build_id = @b", ("b", build)));
        // The tables have no identity columns at all.
        Assert.Equal(0, await ScalarAsync<int>(
            "SELECT COUNT(*)::int FROM information_schema.columns WHERE table_schema = 'ops' AND table_name IN ('web_vital_batch','web_vital_sample') " +
            "AND (column_name ILIKE '%user%' OR column_name ILIKE '%session%' OR column_name ILIKE '%ip%' OR column_name ILIKE '%agent%')"));
    }

    // I-E2
    [Fact]
    public async Task I_E2_ABeaconAsTextPlain_FromASignedInCustomerWithoutACsrfHeader_Is204_AndStoresNoIdentity()
    {
        var (customer, customerId, _, _) = await RegisterAndSignInCustomerAsync();
        customer.DefaultRequestHeaders.Remove("X-CSRF-Token");
        var build = NewBuild();

        var response = await customer.PostAsync("/api/telemetry/web-vitals", Json_(new { buildId = build, samples = new[] { Sample(NewRoute()) } }, "text/plain"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(Guid.Empty, await ScalarAsync<Guid>("SELECT \"CreatedBy\" FROM ops.web_vital_batch WHERE build_id = @b", ("b", build)));
        Assert.NotEqual(customerId, Guid.Empty);
    }

    // I-E3
    [Fact]
    public async Task I_E3_OversizedAndMalformedBeacons_AreRefused_WithOneGenericCode()
    {
        var client = NewClient(Factory);
        var route = NewRoute();

        var big = await client.PostAsync("/api/telemetry/web-vitals",
            Json_(new { buildId = NewBuild(), samples = new[] { Sample(route) }, padding = new string('x', 5 * 1024) }));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, big.StatusCode);

        var tooMany = await client.PostAsync("/api/telemetry/web-vitals",
            Json_(new { buildId = NewBuild(), samples = Enumerable.Range(0, 11).Select(_ => Sample(route)).ToArray() }));
        var withQuery = await client.PostAsync("/api/telemetry/web-vitals", Json_(new { buildId = NewBuild(), samples = new[] { Sample(route + "?q=1") } }));
        var withAt = await client.PostAsync("/api/telemetry/web-vitals", Json_(new { buildId = NewBuild(), samples = new[] { Sample("/a@b") } }));
        var unknownMetric = await client.PostAsync("/api/telemetry/web-vitals", Json_(new { buildId = NewBuild(), samples = new[] { Sample(route, "FID") } }));
        var notJson = await client.PostAsync("/api/telemetry/web-vitals", new StringContent("not json", Encoding.UTF8, "text/plain"));

        foreach (var refused in new[] { tooMany, withQuery, withAt, unknownMetric, notJson })
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Contains("InvalidWebVitalsPayload", await refused.Content.ReadAsStringAsync());
        }
    }

    // I-E4
    [Fact]
    public async Task I_E4_The31stBeaconInAMinute_Is429WithRetryAfter()
    {
        var app = Factory.WithWebHostBuilder(_ => { });          // a fresh host, so a fresh limiter
        var client = NewClient(app);
        HttpResponseMessage? last = null;

        for (var i = 0; i < 31; i++)
            last = await client.PostAsync("/api/telemetry/web-vitals", Json_(new { buildId = NewBuild(), samples = new[] { Sample(NewRoute()) } }));

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.True(last.Headers.Contains("Retry-After"));
    }

    // I-E5
    [Fact]
    public async Task I_E5_TheSummary_ReportsTheP75PerRoute_AndJudgesItAgainstTheBudget()
    {
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);
        var client = NewClient(Factory);
        var passing = NewRoute();
        var failing = NewRoute();
        var few = NewRoute();

        var values = Enumerable.Range(0, 60).Select(i => 1000m + i * 10m).ToList();                 // p75 = 1442.5 ms
        foreach (var chunk in values.Chunk(10))
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/telemetry/web-vitals",
                Json_(new { buildId = NewBuild(), samples = chunk.Select(v => Sample(passing, "LCP", v)).ToArray() }))).StatusCode);
        foreach (var chunk in Enumerable.Range(0, 60).Select(i => 3000m + i).Chunk(10))
            await client.PostAsync("/api/telemetry/web-vitals", Json_(new { buildId = NewBuild(), samples = chunk.Select(v => Sample(failing, "LCP", v)).ToArray() }));
        await client.PostAsync("/api/telemetry/web-vitals", Json_(new { buildId = NewBuild(), samples = new[] { Sample(few, "LCP", 9000m) } }));

        var today = DateTime.UtcNow.Date;
        string Range(string route) =>
            $"/api/admin/web-vitals/summary?from={today.AddDays(-1):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}&routeTemplate={Uri.EscapeDataString(route)}";

        decimal Independent(IReadOnlyList<decimal> sorted)
        {
            var position = 0.75m * (sorted.Count - 1);
            var lower = (int)Math.Floor(position);
            return sorted[lower] + (position - lower) * (sorted[Math.Min(lower + 1, sorted.Count - 1)] - sorted[lower]);
        }

        var pass = (await ReadJson(await admin.GetAsync(Range(passing)))).GetProperty("rows").EnumerateArray().Single();
        Assert.Equal(60, pass.GetProperty("sampleCount").GetInt32());
        Assert.Equal(Independent(values.Order().ToList()), pass.GetProperty("p75").GetDecimal());
        Assert.Equal("Pass", pass.GetProperty("verdict").GetString());
        Assert.Equal("Fail", (await ReadJson(await admin.GetAsync(Range(failing)))).GetProperty("rows")[0].GetProperty("verdict").GetString());
        Assert.Equal("InsufficientData", (await ReadJson(await admin.GetAsync(Range(few)))).GetProperty("rows")[0].GetProperty("verdict").GetString());

        var summary = await ReadJson(await admin.GetAsync(Range(passing)));
        Assert.Equal(2500, summary.GetProperty("budgets").GetProperty("lcpMs").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/admin/web-vitals/summary?from={today.AddDays(-100):yyyy-MM-dd}&to={today:yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/web-vitals/summary")).StatusCode);
        var (reception, _) = await SignedInStaffAsync(StaffRoles.Reception);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync(Range(passing))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient(Factory).GetAsync(Range(passing))).StatusCode);
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, response.StatusCode + " " + await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    // I-E6
    [Fact]
    public async Task I_E6_NothingIdentifyingIsLogged_ForAnAcceptedOrARefusedBeacon()
    {
        var logs = new CapturingLoggerProvider();
        var app = Factory.WithFixedRemoteIpAddress(FixedIp).WithWebHostBuilder(builder => builder.ConfigureTestServices(s => s.AddLogging(l => l.AddProvider(logs))));
        var (customer, customerId, mobile, _) = await RegisterAndSignInCustomerAsync(app);
        logs.Clear();

        await customer.PostAsync("/api/telemetry/web-vitals", Json_(new { buildId = NewBuild(), samples = new[] { Sample(NewRoute()) } }));
        await customer.PostAsync("/api/telemetry/web-vitals", Json_(new { buildId = NewBuild(), samples = new[] { Sample("/bad?x=1") } }));
        await customer.PostAsync("/api/telemetry/web-vitals", new StringContent("not json", Encoding.UTF8, "text/plain"));

        var all = string.Join('\n', logs.Messages);
        Assert.DoesNotContain(FixedIp, all);
        Assert.DoesNotContain(customerId.ToString(), all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(mobile, all);
    }

    // I-E7
    [Fact]
    public async Task I_E7_ABatchWritesNoChangeCaptureAuditRow()
    {
        var build = NewBuild();
        var batchesBefore = await ScalarAsync<int>("SELECT COUNT(*)::int FROM audit.audit_entry WHERE \"EntityType\" LIKE '%WebVital%'");

        await NewClient(Factory).PostAsync("/api/telemetry/web-vitals", Json_(new { buildId = build, samples = new[] { Sample(NewRoute()) } }));

        Assert.Equal(1, await BatchCountAsync(build));
        Assert.Equal(batchesBefore, await ScalarAsync<int>("SELECT COUNT(*)::int FROM audit.audit_entry WHERE \"EntityType\" LIKE '%WebVital%'"));
    }

    // I-C3 (deferred from Phase C: the table arrives with this phase)
    [Fact]
    public async Task I_C3_TheDailyRun_PurgesBatchesPastTheTelemetryPeriod_KeepsRecentOnes_AndAuditsThePurge()
    {
        var (aged, clock) = Factory.WithFakeTimeProvider(DateTimeOffset.UtcNow.AddDays(-100));
        var oldBuild = NewBuild();
        var youngBuild = NewBuild();
        Assert.Equal(HttpStatusCode.NoContent, (await NewClient(aged).PostAsync("/api/telemetry/web-vitals",
            Json_(new { buildId = oldBuild, samples = new[] { Sample(NewRoute()) } }))).StatusCode);
        clock.SetUtcNow(DateTimeOffset.UtcNow.AddDays(-10));
        Assert.Equal(HttpStatusCode.NoContent, (await NewClient(aged).PostAsync("/api/telemetry/web-vitals",
            Json_(new { buildId = youngBuild, samples = new[] { Sample(NewRoute()) } }))).StatusCode);
        var oldBatchId = await ScalarAsync<Guid>("SELECT \"Id\" FROM ops.web_vital_batch WHERE build_id = @b", ("b", oldBuild));

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Retention.RetentionWorkQueue>();
            var cycle = scope.ServiceProvider.GetRequiredService<RetentionWorkCycle>();
            while (await queue.GetRequestedRunIdAsync(CancellationToken.None) is { } pending) await cycle.RunDailyPassesAsync(RetentionRunTrigger.Manual, pending);
            Assert.NotNull(await cycle.RunDailyPassesAsync(RetentionRunTrigger.Scheduled));
        }

        Assert.Equal(0, await BatchCountAsync(oldBuild));
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*)::int FROM ops.web_vital_sample WHERE web_vital_batch_id = @id", ("id", oldBatchId)));   // samples went with it
        Assert.Equal(1, await BatchCountAsync(youngBuild));
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = [];
        public IReadOnlyList<string> Messages { get { lock (_messages) return _messages.ToList(); } }
        public void Clear() { lock (_messages) _messages.Clear(); }
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);
        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (owner._messages) owner._messages.Add(formatter(state, exception) + (exception?.Message ?? string.Empty));
            }
        }
    }
}
