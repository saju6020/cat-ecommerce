using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.QueryHandlers.Reporting;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-RPT-001 section 16. Real HTTP through TestServer, against this branch's own <c>catecommerce_rpt</c> database (set <c>CATECOMMERCE_TEST_DB</c> to its FULL Npgsql connection string). It builds on
/// <see cref="BillingApiFactory"/> (a non-ticking <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/>, the capturing email and SMS senders, every worker off), and adds the
/// <b>fixture reports</b> of <see cref="ReportingFixtures"/> -- a report that returns a chosen number of rows, one that sleeps and one that tries to write -- so the framework is tested without
/// depending on any other area's data. The Reporting tests create source data through the owning areas' own endpoints (section 16.1 rule 3) and never insert into their tables.
/// </summary>
public class ReportingApiFactory : BillingApiFactory
{
    public ReportingApiFactory()
    {
        ReportingTestDatabase.Require();
    }

    // A scheduled report is stored through HO-FIL-001 (ReportExport): the signing key and an isolated local store, so a run never touches another run's or another database's objects.
    private readonly string _isolatedFileStoreRoot = Path.Combine(Path.GetTempPath(), "catecommerce-file-store-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureAdditionalSettings(IDictionary<string, string?> settings)
    {
        base.ConfigureAdditionalSettings(settings);
        settings["Reporting:Worker:Enabled"] = "false";
        settings["FileStorage:UrlSigningKey"] = "test-environment-signing-key-at-least-32-bytes-long";
        settings["FileStorage:LocalStoreRoot"] = Path.Combine(_isolatedFileStoreRoot, "store");
        settings["FileStorage:TempPath"] = Path.Combine(_isolatedFileStoreRoot, "temp");
    }

    /// <summary>Every log line written in this host ("category|level|message exception"), for I-X1: no row data, contact detail or file content may be in any of them.</summary>
    public IReadOnlyCollection<string> LoggedLines => Logged.ToArray();

    private static readonly System.Collections.Concurrent.ConcurrentQueue<string> Logged = new();

    private sealed class CapturingLoggerProvider : Microsoft.Extensions.Logging.ILoggerProvider
    {
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName);

        public void Dispose() { }

        private sealed class CapturingLogger(string category) : Microsoft.Extensions.Logging.ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => logLevel >= Microsoft.Extensions.Logging.LogLevel.Information;

            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel)) Logged.Enqueue($"{category}|{logLevel}|{formatter(state, exception)} {exception}");
            }
        }
    }

    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        base.ConfigureAdditionalServices(services);
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider, CapturingLoggerProvider>();
        ReportingFixtures.Register(services);

        // U-E2: every tile source is decorated so a test can prove which tiles were computed for which caller.
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(CatEcommerce.Read.QueryHandlers.Reporting.Dashboard.IDashboardTileSource) && d.ImplementationType is not null).ToList())
        {
            var implementation = descriptor.ImplementationType!;
            services.AddScoped<CatEcommerce.Read.QueryHandlers.Reporting.Dashboard.IDashboardTileSource>(
                sp => new CountingTile((CatEcommerce.Read.QueryHandlers.Reporting.Dashboard.IDashboardTileSource)ActivatorUtilities.CreateInstance(sp, implementation)));
        }
    }

    /// <summary>The (tile key, caller id) of every tile computed in this host.</summary>
    public IEnumerable<(string Key, Guid CallerId)> ComputedTiles => Computed;

    private sealed class CountingTile(CatEcommerce.Read.QueryHandlers.Reporting.Dashboard.IDashboardTileSource inner) : CatEcommerce.Read.QueryHandlers.Reporting.Dashboard.IDashboardTileSource
    {
        public string Key => inner.Key;

        public Task<CatEcommerce.Read.QueryHandlers.Reporting.Dashboard.DashboardTileResult> RunAsync(CatEcommerce.Read.QueryHandlers.Reporting.Dashboard.DashboardContext context, CancellationToken ct)
        {
            Computed.Add((inner.Key, context.CallerId));
            return inner.RunAsync(context, ct);
        }
    }

    internal static readonly System.Collections.Concurrent.ConcurrentBag<(string Key, Guid CallerId)> Computed = [];
}

/// <summary>
/// The Reporting tests change and cancel whole tables (the schedule tests retire every active schedule), so they run only against a branch-owned database. Refuses an unset variable, a value that is
/// not a connection string, <c>catecommerce_dev</c>, <c>catecommerce_test</c> (and its dated copies) and the bare shared <c>catecommerce</c>.
/// </summary>
public static class ReportingTestDatabase
{
    public static string Require() => Validate(Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB"));

    public static string Validate(string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("CATECOMMERCE_TEST_DB is not set. Set it to the FULL Npgsql connection string of this branch's own database (for example catecommerce_rpt).");

        string database;
        try { database = new Npgsql.NpgsqlConnectionStringBuilder(connection).Database ?? ""; }
        catch (ArgumentException) { throw new InvalidOperationException("CATECOMMERCE_TEST_DB must be a full Npgsql connection string (Host=...;Database=...;...)."); }

        if (string.IsNullOrWhiteSpace(database) || System.Text.RegularExpressions.Regex.IsMatch(database, "^catecommerce(_dev|_test.*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new InvalidOperationException($"The Reporting tests must never run against '{database}': use this branch's own database (catecommerce_rpt), never catecommerce_dev, catecommerce_test or the shared catecommerce.");

        return connection;
    }
}

/// <summary>The fixture reports, registered only in the test host (never in production code).</summary>
public static class ReportingFixtures
{
    public const string RowsKey = "fixture-rows";
    public const string SleepKey = "fixture-sleep";
    public const string WriteKey = "fixture-write";

    /// <summary>The row limit the last <c>fixture-rows</c> run was given -- U-A11 asserts the pipeline passes cap + 1, not "everything".</summary>
    public static System.Collections.Concurrent.ConcurrentDictionary<int, int> RowLimitByRows { get; } = new();

    public static readonly ReportDefinition Rows = new(
        RowsKey, "Fixture rows", "Returns a chosen number of rows.", ReportCategory.ClinicOperations, ReportKind.Period, ReportRelativePeriod.Today, 366,
        [new ReportParameter("rows", "Rows", ReportParameterType.Integer, "3", Min: 0, Max: 100_000)],
        [
            new ReportColumn("name", "Name", ReportColumnType.Text),
            new ReportColumn("amount", "Amount", ReportColumnType.Money),
            new ReportColumn("secret", "Secret", ReportColumnType.Money, RestrictedToAction: Actions.Reports.FinancialView),
            new ReportColumn("count", "Count", ReportColumnType.Integer),
        ],
        "name", true, false, ["fixture"]);

    public static readonly ReportDefinition Sleep = new(
        SleepKey, "Fixture sleep", "Sleeps inside the read-only scope.", ReportCategory.ClinicOperations, ReportKind.Period, ReportRelativePeriod.Today, 366,
        [new ReportParameter("seconds", "Seconds", ReportParameterType.Integer, "2", Min: 0, Max: 30)],
        [new ReportColumn("name", "Name", ReportColumnType.Text)], null, false, false, ["fixture"]);

    public static readonly ReportDefinition Write = new(
        WriteKey, "Fixture write", "Tries to write inside the read-only scope.", ReportCategory.ClinicOperations, ReportKind.Period, ReportRelativePeriod.Today, 366,
        [], [new ReportColumn("name", "Name", ReportColumnType.Text)], null, false, false, ["fixture"]);

    public static IReadOnlyList<ReportDefinition> All => [Rows, Sleep, Write];

    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<IReportCatalogue>(new FixtureCatalogue());
        services.AddScoped<IReportSource, RowsSource>();
        services.AddScoped<IReportSource, SleepSource>();
        services.AddScoped<IReportSource, WriteSource>();
    }

    private sealed class FixtureCatalogue : IReportCatalogue
    {
        public IReadOnlyList<ReportDefinition> All { get; } = ReportCatalogue.All.Concat(ReportingFixtures.All).ToList();

        public ReportDefinition? Find(string key) => All.FirstOrDefault(d => d.Key == key);
    }

    private sealed class RowsSource : IReportSource
    {
        public string Key => RowsKey;

        public Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
        {
            RowLimitByRows[request.Number("rows")] = request.RowLimit; // keyed by the rows asked for, so parallel tests cannot overwrite each other
            var count = Math.Min(request.Number("rows"), request.RowLimit);

            var rows = Enumerable.Range(1, count).Select(i =>
            {
                var row = new ReportRow
                {
                    ["name"] = i switch { 1 => "=HYPERLINK(\"http://x\")", 2 => "বিড়াল", _ => $"Row {i}" },
                    ["amount"] = new ReportMoney(i * 10.5m, "BDT"),
                    ["secret"] = new ReportMoney(i, "BDT"),
                    ["count"] = (long)i,
                };
                return row;
            }).ToList();

            var totals = new ReportRow { ["amount"] = new ReportMoney(rows.Sum(r => ((ReportMoney)r["amount"]!).Amount), "BDT"), ["secret"] = new ReportMoney(1, "BDT") };
            return Task.FromResult(new ReportSourceResult(rows, totals));
        }
    }

    private sealed class SleepSource : IReportSource
    {
        public string Key => SleepKey;

        public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
        {
            await db.Database.ExecuteSqlRawAsync($"select pg_sleep({request.Number("seconds")})", ct);
            return new ReportSourceResult([new ReportRow { ["name"] = "slept" }]);
        }
    }

    private sealed class WriteSource : IReportSource
    {
        public string Key => WriteKey;

        public async Task<ReportSourceResult> RunAsync(ReportRequest request, CatEcommerceDbContext db, CancellationToken ct)
        {
            // ADR-279: a write inside the scope must fail in the database, whatever the code does. rv_branch is an auto-updatable view -- the case F-RPT-1 names.
            await db.Database.ExecuteSqlRawAsync(
                "insert into report.rv_branch (branch_id, code, name, time_zone_id, is_active) values (gen_random_uuid(), 'X', 'X', 'UTC', true)", ct);
            return new ReportSourceResult([new ReportRow { ["name"] = "wrote" }]);
        }
    }
}
