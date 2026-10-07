// FileStorage module -- HO-FIL-001.
//
// Registers the upload pipeline's domain services (IFileStore, IMalwareScanner, IFileUrlSigner,
// IRetentionPurgeSource), the write/read handlers and the environment-gated adapter selection
// (§8.4/§8.5/§12). Moved out of Program.cs by ADR-341 so this area never needs to touch a file
// another area also edits.

using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.CommandHandlers.StoredFile;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.StoredFile;
using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Files;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.StoredFile;
using CatEcommerce.Read.QueryHandlers.StoredFile;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Queries;
using RateLimitPartition = System.Threading.RateLimiting.RateLimitPartition;

namespace CatEcommerce.Api.Modules;

public static class FileStorageModule
{
    public static IServiceCollection AddFileStorageModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<FileStorageStartupGuardHostedService>();

        services.AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.SectionName))
            .ValidateOnStart();

        services.TryAddTimeProvider();

        // ── IFileStore (§8.4): LocalFileStore in Development/Testing, GcsFileStore elsewhere ────
        services.AddScoped<LocalFileStore>();
        services.AddScoped<GcsFileStore>();
        // ADC -- no key file on disk (V2). StorageClient is thread-safe; one Singleton instance,
        // constructed lazily on first resolution -- which only ever happens via GcsFileStore
        // (resolved only outside Development/Testing, see the IFileStore factory below) or the
        // startup guard's own explicit resolution outside Development/Testing -- so it is never
        // invoked, and never throws for missing ADC, in Development/Testing.
        services.AddSingleton(_ => StorageClient.Create());
        services.AddScoped<IGcsBucketPolicyChecker, GcsBucketPolicyChecker>();
        services.AddScoped<IFileStore>(sp =>
        {
            var environment = sp.GetRequiredService<IHostEnvironment>();
            return environment.IsDevelopment() || environment.EnvironmentName == "Testing"
                ? sp.GetRequiredService<LocalFileStore>()
                : sp.GetRequiredService<GcsFileStore>();
        });

        // ── IMalwareScanner (§8.5, ADR-319) ─────────────────────────────────────────────────────
        services.AddScoped<DevelopmentNoOpScanner>();
        services.AddScoped<EicarTestScanner>();
        services.AddScoped<ClamAvScanner>();
        services.AddScoped<UnconfiguredMalwareScanner>();
        services.AddScoped<IMalwareScanner>(sp =>
        {
            var environment = sp.GetRequiredService<IHostEnvironment>();
            if (environment.IsDevelopment()) return sp.GetRequiredService<DevelopmentNoOpScanner>();
            if (environment.EnvironmentName == "Testing") return sp.GetRequiredService<EicarTestScanner>();

            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FileStorageOptions>>().Value;
            return string.IsNullOrWhiteSpace(options.ClamAvHost)
                ? sp.GetRequiredService<UnconfiguredMalwareScanner>()
                : sp.GetRequiredService<ClamAvScanner>();
        });

        // ── Serving (§8.6) ───────────────────────────────────────────────────────────────────────
        services.AddSingleton<HmacFileUrlSigner>();
        services.AddSingleton<IFileUrlSigner>(sp => sp.GetRequiredService<HmacFileUrlSigner>());

        // ── Purpose access (§9 OQ-7, gate-fix 2026-09-27 SECURITY MEDIUM: fails closed) ─────────
        services.AddScoped<IFilePurposeAccessPolicy, DefaultFilePurposeAccessPolicy>();

        // ── Deletion and retention (§8.7, HO-XCT-001's contract) ────────────────────────────────
        services.AddScoped<IStoredFileEraser, StoredFileEraser>();
        // Gate-fix 2026-09-27 (CODE_REVIEW LOW / ARCHITECTURE LOW): one purge source per purpose,
        // rather than one source covering every purpose with a single shared cutoff -- so a future
        // retention worker can give each purpose its own retention window (§8.7's own "given a
        // purpose and a cut-off").
        foreach (var purpose in Enumeration.GetAll<FilePurpose>())
        {
            var capturedPurpose = purpose;
            services.AddScoped<IRetentionPurgeSource>(sp => new StoredFilePurgeSource(
                capturedPurpose,
                sp.GetRequiredService<CatEcommerceDbContext>(),
                sp.GetRequiredService<IStoredFileEraser>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<StoredFilePurgeSource>>()));
        }

        // ── C-ALL-2: this context stores a customer's personal data beyond the id (UploadedBy,
        // OriginalFileName) for any customer-uploaded file -- gate-fix 2026-09-27 (ARCHITECTURE HIGH).
        services.AddScoped<IPersonalDataEraser, FilesPersonalDataEraser>();

        // ── Command handlers + validators ───────────────────────────────────────────────────────
        services.AddScoped<ICommandHandlerAsync<RegisterStoredFileCommand>, RegisterStoredFileCommandHandler>();
        services.AddScoped<IValidator<RegisterStoredFileCommand>, RegisterStoredFileCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<DeleteStoredFileCommand>, DeleteStoredFileCommandHandler>();
        services.AddScoped<IValidator<DeleteStoredFileCommand>, DeleteStoredFileCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordFileRejectionAuditCommand>, RecordFileRejectionAuditCommandHandler>();
        services.AddScoped<IValidator<RecordFileRejectionAuditCommand>, RecordFileRejectionAuditCommandValidator>();

        // ── Query handlers ───────────────────────────────────────────────────────────────────────
        // Gate-fix 2026-09-27 (ARCHITECTURE LOW, second pass): FIL's own "ids -> §12.4 signed
        // shapes" helper, consumed here and by CatProfilesModule's GetCatProfileQueryHandler.
        services.AddScoped<IStoredFileSigningBatcher, StoredFileSigningBatcher>();
        services.AddScoped<IQueryHandlerAsync<GetStoredFileQuery, StoredFileViewModel?>, GetStoredFileQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStoredFileForServingQuery, StoredFileServingInfo?>, GetStoredFileForServingQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetFilesForPurgeQuery, IReadOnlyList<PurgeEligibleFileViewModel>>, GetFilesForPurgeQueryHandler>();

        // ── Rate limiting (gate-fix 2026-09-27, SECURITY/LOW; policy names/config-binding moved to
        // this area's own file, ARCHITECTURE second pass MEDIUM). A second AddRateLimiter(...) call
        // is additive (ASP.NET Core's own extension does services.Configure<RateLimiterOptions>,
        // which stacks every configure delegate registered against it) -- this does not need
        // Program.cs or AccountsModule touched. UseRateLimiter() is already called once, directly
        // in Program.cs's own shared host-composition block (moved out of
        // AccountsModule.UseAccountsModule() by HO-NTF-001-ARCHITECTURE second pass MEDIUM,
        // integrator-authorised, once Notifications became a second area contributing its own
        // rate-limit policies -- see .ai/host-composition.md). The numbers come from the shared "RateLimiting"
        // config section (matching Accounts' own RateLimitingOptions), read directly here rather
        // than by adding File-owned properties to Accounts' RateLimitingOptions class -- the exact
        // scope problem this gate-fix exists to close.
        //
        // The GetSection(...).Get<...>() calls are made INSIDE the AddRateLimiter(options => ...)
        // delegate, not before it, and deliberately so: that delegate is a configure-options
        // callback (services.Configure<RateLimiterOptions>(configureOptions)), evaluated LAZILY the
        // first time RateLimiterOptions is materialised, not at this method's own call time. Reading
        // configuration eagerly here (as a local variable computed before AddRateLimiter is called)
        // was tried and found to bind stale values in one specific circumstance: a config source
        // added by a WebApplicationFactory customisation AFTER this module's own registration call
        // returns (proven by a startup-time diagnostic: the eager read saw the appsettings.json
        // default even though IConfiguration resolved from the same running host's own DI container
        // afterward already showed the overridden value) -- AccountsModule's identical-looking
        // RateLimitingOptions read already does this correctly by reading inside its own delegate;
        // this mirrors that, not a new pattern. ─────────────────────────────────────────────────────
        services.AddRateLimiter(options =>
        {
            var fileUploadLimit = configuration.GetSection("RateLimiting:FileUpload").Get<RateLimitPolicyOptions>()
                ?? new RateLimitPolicyOptions { PermitLimit = 20, WindowMinutes = 10 };
            var fileServeLimit = configuration.GetSection("RateLimiting:FileServe").Get<RateLimitPolicyOptions>()
                ?? new RateLimitPolicyOptions { PermitLimit = 60, WindowMinutes = 1 };

            options.AddPolicy(FileRateLimitPolicies.FileUpload, context =>
            {
                var caller = context.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller;
                var partitionKey = caller is { Kind: not ResolvedCallerKind.Anonymous }
                    ? $"caller:{caller.AccountId}"
                    : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = fileUploadLimit.PermitLimit,
                    Window = TimeSpan.FromMinutes(fileUploadLimit.WindowMinutes),
                    QueueLimit = 0
                });
            });

            options.AddPolicy(FileRateLimitPolicies.FileServe, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = fileServeLimit.PermitLimit,
                        Window = TimeSpan.FromMinutes(fileServeLimit.WindowMinutes),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        // Accounts already registers this (TryAddSingleton), but a module must not assume load
        // order -- TryAddSingleton is idempotent regardless of which module runs first.
        services.AddOptions();
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions
            .TryAddSingleton(services, TimeProvider.System);
    }
}

/// <summary>
/// §20.1/§20.2/§8.4/§8.6: startup refuses a configuration that would silently misbehave outside
/// Development/Testing. Runs at real host startup (an <see cref="IHostedService"/> throwing from
/// <see cref="StartAsync"/> aborts host startup, for both <c>dotnet run</c> and
/// <c>WebApplicationFactory</c>-driven tests), mirroring <c>AccountStartupGuardHostedService</c>
/// (HO-ACC-001).
///
/// Gate-fix 2026-09-27 (ARCHITECTURE HIGH): previously checked only the bucket name and the
/// signing key. Now also, outside Development/Testing:
/// - forces <see cref="StorageClient"/> construction (via <see cref="IServiceProvider"/>, not a
///   constructor dependency, so Development/Testing still never constructs it) -- ADC that does
///   not resolve now fails startup instead of surfacing as the first request's 500;
/// - asserts the bucket's own security posture via <see cref="IGcsBucketPolicyChecker"/> (ADR-318:
///   uniform bucket-level access, public-access prevention enforced, versioning off, soft-delete
///   off).
/// </summary>
internal sealed class FileStorageStartupGuardHostedService(
    IHostEnvironment environment,
    IServiceProvider serviceProvider,
    Microsoft.Extensions.Options.IOptions<FileStorageOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var isDevelopmentOrTesting = environment.IsDevelopment() || environment.EnvironmentName == "Testing";
        var settings = options.Value;

        if (!isDevelopmentOrTesting)
        {
            if (string.IsNullOrWhiteSpace(settings.GcsBucketName))
                throw new InvalidOperationException("FileStorage:GcsBucketName must be set outside Development/Testing (§20.1).");

            if (settings.GcsBucketName.Contains("prod", StringComparison.OrdinalIgnoreCase) && environment.EnvironmentName != "Production")
                throw new InvalidOperationException(
                    "FileStorage:GcsBucketName looks like a production bucket name but the environment is not Production (§8.4).");

            if (string.IsNullOrWhiteSpace(settings.UrlSigningKey) || Encoding8Length(settings.UrlSigningKey) < 32)
                throw new InvalidOperationException("FileStorage:UrlSigningKey must be set and at least 32 bytes outside Development/Testing (§8.6).");

            // Resolved here, not via constructor injection, so Development/Testing never triggers
            // StorageClient.Create() (which requires ADC) at all.
            using var scope = serviceProvider.CreateScope();
            _ = scope.ServiceProvider.GetRequiredService<StorageClient>(); // throws if ADC does not resolve (V2)

            var checker = scope.ServiceProvider.GetRequiredService<IGcsBucketPolicyChecker>();
            await checker.AssertSecureAsync(settings.GcsBucketName, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static int Encoding8Length(string value) => System.Text.Encoding.UTF8.GetByteCount(value);
}
