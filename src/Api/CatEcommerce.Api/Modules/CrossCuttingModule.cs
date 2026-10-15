// CrossCutting module -- HO-XCT-001 (Operations context, ADR-297).
//
// Registers the options, the startup role guard, the retention erasers/review sources, the Operations command handlers, validators, query
// handlers, domain event handlers, the retention work cycle and (Phase C) the retention worker. Program.cs already calls
// AddCrossCuttingModule(builder.Configuration) (ADR-341); this file is the only one that changes per phase.
//
// Registered by hand, one line per type (there is no assembly scanning): validators as IValidator<TCommand> (not AbstractValidator<>, or they
// silently never run), erasers/review sources/purge sources as MULTIPLE Scoped registrations of the same interface, consumed as IEnumerable<T>.

using CatEcommerce.Api.Background;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.CommandHandlers.Operations;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Application.DomainEventHandlers.Operations;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Events.Operations;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Operations;
using CatEcommerce.Infrastructure.Retention;
using CatEcommerce.Infrastructure.Retention.Erasers;
using CatEcommerce.Read.Queries.Operations;
using CatEcommerce.Read.QueryHandlers.Operations;
using CatEcommerce.Read.ViewModels;
using FluentValidation;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Events;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Api.Modules;

public static class CrossCuttingModule
{
    public static IServiceCollection AddCrossCuttingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // ── Options (section 12.2) -- no secret in this section ───────────────────────────────────
        services.AddOptions<OperationsOptions>()
            .Bind(configuration.GetSection(OperationsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // ── Startup role guard (ADR-300): runs once at host start; never applies a migration ──────
        services.AddSingleton<DatabaseRoleGuard>();
        services.AddHostedService<DatabaseRoleGuardHostedService>();

        // ── Ports ──────────────────────────────────────────────────────────────────────────────
        services.AddScoped<IOperationsClock, OperationsClock>();

        // The Account eraser; Notification and Administration register their own in their own modules (C-ALL-2).
        services.AddScoped<IPersonalDataEraser, AccountPersonalDataEraser>();

        RegisterCommands(services);
        RegisterQueries(services);
        RegisterDomainEventHandlers(services);

        // ── Retention work (ADR-303) ─────────────────────────────────────────────────────────────
        services.AddScoped<RetentionWorkQueue>();
        services.AddScoped<RetentionWorkCycle>();
        services.AddScoped<IRetentionPurger, RetentionPurger>();
        services.AddScoped<IRetentionReviewSource, CatClinicalReviewSource>();
        services.AddHostedService<RetentionWorker>();

        // ── Offline front-desk snapshot (ADR-306): one registration per section source; boardingTasks has none until Boarding exists ──
        services.AddScoped<IFrontDeskSnapshotSource, ClinicDayListSnapshotSource>();

        // ── Rate limits (section 12.5): per IP, fixed window, read lazily so a test can override them ──
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitPolicies.Ping, context => IpLimiter(context, "Ping", 120));
            options.AddPolicy(RateLimitPolicies.Telemetry, context => IpLimiter(context, "Telemetry", 30));
        });

        return services;
    }

    private static System.Threading.RateLimiting.RateLimitPartition<string> IpLimiter(Microsoft.AspNetCore.Http.HttpContext context, string name, int defaultPermits)
    {
        var section = context.RequestServices.GetRequiredService<IConfiguration>().GetSection($"RateLimiting:{name}");
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            { PermitLimit = section.GetValue("PermitLimit", defaultPermits), Window = TimeSpan.FromMinutes(section.GetValue("WindowMinutes", 1)), QueueLimit = 0 });
    }

    private static void RegisterCommands(IServiceCollection services)
    {
        services.AddScoped<ICommandHandlerAsync<OpenErasureRequestCommand>, OpenErasureRequestCommandHandler>();
        services.AddScoped<IValidator<OpenErasureRequestCommand>, OpenErasureRequestCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ExecuteErasureScopeCommand>, ExecuteErasureScopeCommandHandler>();
        services.AddScoped<IValidator<ExecuteErasureScopeCommand>, ExecuteErasureScopeCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<EraseCustomerOnRequestCommand>, EraseCustomerOnRequestCommandHandler>();
        services.AddScoped<IValidator<EraseCustomerOnRequestCommand>, EraseCustomerOnRequestCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ReapplyErasuresAfterRestoreCommand>, ReapplyErasuresAfterRestoreCommandHandler>();
        services.AddScoped<IValidator<ReapplyErasuresAfterRestoreCommand>, ReapplyErasuresAfterRestoreCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordOfflineSnapshotIssuedCommand>, RecordOfflineSnapshotIssuedCommandHandler>();
        services.AddScoped<IValidator<RecordOfflineSnapshotIssuedCommand>, RecordOfflineSnapshotIssuedCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordWebVitalsCommand>, RecordWebVitalsCommandHandler>();
        services.AddScoped<IValidator<RecordWebVitalsCommand>, RecordWebVitalsCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordRestoreTestCommand>, RecordRestoreTestCommandHandler>();
        services.AddScoped<IValidator<RecordRestoreTestCommand>, RecordRestoreTestCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RequestRetentionRunCommand>, RequestRetentionRunCommandHandler>();
        services.AddScoped<IValidator<RequestRetentionRunCommand>, RequestRetentionRunCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<StartRetentionRunCommand>, StartRetentionRunCommandHandler>();
        services.AddScoped<IValidator<StartRetentionRunCommand>, StartRetentionRunCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordRetentionPassCommand>, RecordRetentionPassCommandHandler>();
        services.AddScoped<IValidator<RecordRetentionPassCommand>, RecordRetentionPassCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<FinishRetentionRunCommand>, FinishRetentionRunCommandHandler>();
        services.AddScoped<IValidator<FinishRetentionRunCommand>, FinishRetentionRunCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<PurgeExpiredRecordsCommand>, PurgeExpiredRecordsCommandHandler>();
        services.AddScoped<IValidator<PurgeExpiredRecordsCommand>, PurgeExpiredRecordsCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RunPurgeSourceCommand>, RunPurgeSourceCommandHandler>();
        services.AddScoped<IValidator<RunPurgeSourceCommand>, RunPurgeSourceCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RedactExpiredNotificationContentCommand>, RedactExpiredNotificationContentCommandHandler>();
        services.AddScoped<IValidator<RedactExpiredNotificationContentCommand>, RedactExpiredNotificationContentCommandValidator>();
    }

    private static void RegisterQueries(IServiceCollection services)
    {
        services.AddScoped<IQueryHandlerAsync<GetErasureRequestsQuery, PagedResult<ErasureRequestViewModel>>, GetErasureRequestsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetErasureRequestQuery, ErasureRequestViewModel?>, GetErasureRequestQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetFrontDeskOfflineSnapshotQuery, FrontDeskSnapshotViewModel?>, GetFrontDeskOfflineSnapshotQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetWebVitalsSummaryQuery, WebVitalsSummaryViewModel>, GetWebVitalsSummaryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBackupStatusQuery, BackupStatusViewModel>, GetBackupStatusQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRestoreTestsQuery, PagedResult<RestoreTestSummaryViewModel>>, GetRestoreTestsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRestoreTestQuery, RestoreTestViewModel?>, GetRestoreTestQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRetentionScheduleQuery, RetentionScheduleViewModel>, GetRetentionScheduleQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRetentionRunsQuery, PagedResult<RetentionRunSummaryViewModel>>, GetRetentionRunsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRetentionRunQuery, RetentionRunViewModel?>, GetRetentionRunQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRetentionReviewQuery, RetentionReviewViewModel>, GetRetentionReviewQueryHandler>();
    }

    private static void RegisterDomainEventHandlers(IServiceCollection services)
    {
        services.AddScoped<IEventHandlerAsync<CustomerAccountAnonymisedEvent>, OpenErasureRequestOnCustomerAnonymisedHandler>();
        services.AddScoped<IEventHandlerAsync<ErasureRequestCompletedEvent>, AuditErasureRequestCompletedHandler>();
        services.AddScoped<IEventHandlerAsync<RetentionRunFinishedEvent>, AuditRetentionRunFinishedHandler>();
    }
}
