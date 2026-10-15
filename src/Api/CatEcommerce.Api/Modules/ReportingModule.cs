// Reporting module -- HO-RPT-001.
//
// Reports READ migration-owned SQL views in the `report` schema inside a READ ONLY transaction (ADR-278, ADR-279); they write to no other module. Registers every command handler, validator (as
// IValidator<T>, never the AbstractValidator base), query handler, report source and dashboard-tile source explicitly -- there is no assembly scanning -- so a missing registration fails at startup
// (ValidateOnBuild), and U-A2 proves the sources match the catalogue one to one. Program.cs already calls AddReportingModule(builder.Configuration).

using System.Threading.RateLimiting;
using CatEcommerce.Api.Background;
using CatEcommerce.Api.Filters;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.CommandHandlers.Reporting;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Reporting;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Notifications;
using CatEcommerce.Infrastructure.Notifications.Guards;
using CatEcommerce.Infrastructure.Reporting;
using CatEcommerce.Read.Queries.Reporting;
using CatEcommerce.Read.QueryHandlers.Reporting;
using CatEcommerce.Read.QueryHandlers.Reporting.Dashboard;
using CatEcommerce.Read.QueryHandlers.Reporting.Sources;
using CatEcommerce.Read.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Api.Modules;

public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ReportingOptions>()
            .Bind(configuration.GetSection(ReportingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterPorts(services);
        RegisterFramework(services);
        RegisterReportSources(services);
        RegisterDashboard(services);
        RegisterSchedules(services);
        RegisterRateLimiting(services, configuration);

        // The 400 ReportTimedOut mapping (section 12.5). Additive: an MVC filter, registered once.
        services.Configure<MvcOptions>(options => options.Filters.Add<ReportTimedOutExceptionFilter>());

        return services;
    }

    private static void RegisterPorts(IServiceCollection services)
    {
        services.AddScoped<IReportingReadScope, ReportingReadScope>();
        services.AddSingleton<IReportCatalogue, StaticReportCatalogue>();

        // Stateless, so singletons. The same two renderers produce an interactive export and a scheduled attachment (I-G2).
        services.AddSingleton<IReportExportRenderer, ReportCsvWriter>();
        services.AddSingleton<IReportExportRenderer, QuestPdfReportRenderer>();
    }

    private static void RegisterFramework(IServiceCollection services)
    {
        services.AddScoped<IQueryHandlerAsync<RunReportQuery, ReportOutcome>, RunReportQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetReportCatalogueQuery, IReadOnlyList<ReportCatalogueEntryViewModel>>, GetReportCatalogueQueryHandler>();

        // C1 -- the export audit.
        services.AddScoped<ICommandHandlerAsync<RecordReportExportCommand>, RecordReportExportCommandHandler>();
        services.AddScoped<IValidator<RecordReportExportCommand>, RecordReportExportCommandValidator>();
    }

    /// <summary>One <c>IReportSource</c> per catalogue key (U-A2 enforces the 1:1). Each phase adds its sources here.</summary>
    private static void RegisterReportSources(IServiceCollection services)
    {
        // Phase B -- clinic (US-RPT-01 AC1).
        services.AddScoped<IReportSource, ClinicDailyScheduleSource>();
        services.AddScoped<IReportSource, ClinicQueuePerformanceSource>();
        services.AddScoped<IReportSource, SurgeryRegisterSource>();
        services.AddScoped<IReportSource, VaccinationDueListSource>();

        // Phase C -- stock (US-RPT-02).
        services.AddScoped<IReportSource, StockPositionSource>();
        services.AddScoped<IReportSource, StockNearExpirySource>();
        services.AddScoped<IReportSource, StockLowReorderSource>();
        services.AddScoped<IReportSource, ImportConsignmentsSource>();

        // Phase D -- revenue, margin and payments (US-RPT-03, SRS section 8.2).
        services.AddScoped<IReportSource, RevenueByBusinessLineSource>();
        services.AddScoped<IReportSource, RevenueShopByCategorySource>();
        services.AddScoped<IReportSource, RevenueShopByOriginSource>();
        services.AddScoped<IReportSource, RevenueByServiceSource>();
        services.AddScoped<IReportSource, PaymentReconciliationSource>();
        services.AddScoped<IReportSource, DailyCashUpSource>();
        services.AddScoped<IReportSource, ReceivablesAgeingSource>();
        services.AddScoped<IReportSource, RefundsCreditNotesSource>();
        services.AddScoped<IReportSource, TaxSummarySource>();

        // Phase F -- growth (US-RPT-04). R21 (HO-SUB-001), R22 (needs the boarding line) and R23 (no cart reminders in HO-ORD-001) are not delivered yet.
        services.AddScoped<IReportSource, CustomersNewVsReturningSource>();
        services.AddScoped<IReportSource, CustomerLifetimeValueSource>();
        services.AddScoped<IReportSource, CohortRetentionSource>();

        // Phase H -- R27 only. R24-R26 read HO-BRD-001 (not merged) and are not delivered yet.
        services.AddScoped<IReportSource, OrderFulfilmentSource>();
    }

    /// <summary>One IDashboardTileSource per tile key of DashboardTiles (U-A2 enforces the 1:1). The three boarding tiles have no source: HO-BRD-001 is not merged, so they are absent.</summary>
    private static void RegisterDashboard(IServiceCollection services)
    {
        services.AddScoped<IQueryHandlerAsync<GetDashboardQuery, DashboardOutcome>, GetDashboardQueryHandler>();

        services.AddScoped<IDashboardTileSource, AppointmentsTodayTile>();
        services.AddScoped<IDashboardTileSource, QueueNowTile>();
        services.AddScoped<IDashboardTileSource, MyAppointmentsTodayTile>();
        services.AddScoped<IDashboardTileSource, VaccinationsDueTile>();
        services.AddScoped<IDashboardTileSource, ConsultationsUnfinalisedTile>();
        services.AddScoped<IDashboardTileSource, SurgeryTodayTile>();
        services.AddScoped<IDashboardTileSource, StockExpiryTile>();
        services.AddScoped<IDashboardTileSource, StockLowTile>();
        services.AddScoped<IDashboardTileSource, ConsignmentsOpenTile>();
        services.AddScoped<IDashboardTileSource, OrdersTodayTile>();
        services.AddScoped<IDashboardTileSource, OrdersAwaitingTransferTile>();
        services.AddScoped<IDashboardTileSource, OrdersReturnsPendingTile>();
        services.AddScoped<IDashboardTileSource, ShopRevenueTodayTile>();
        services.AddScoped<IDashboardTileSource, PaymentsTodayTile>();
        services.AddScoped<IDashboardTileSource, PaymentsUnreconciledTile>();
        services.AddScoped<IDashboardTileSource, CashUpTodayTile>();
        services.AddScoped<IDashboardTileSource, ReceivablesTile>();
        services.AddScoped<IDashboardTileSource, RevenueTodayByLineTile>();
    }

    /// <summary>
    /// Phase G (US-RPT-06): the schedule commands, validators and reads, the amendment's guard and attachment source, and the worker. The worker is disabled in Testing (<c>Reporting:Worker:Enabled=false</c>); tests
    /// call <c>ReportingWorkCycle</c> directly. The notification attachment <i>resolver</i> is registered HERE, with its only source: HO-NTF-001's deliver handler depends on this registration (ValidateOnBuild fails if it is removed).
    /// </summary>
    private static void RegisterSchedules(IServiceCollection services)
    {
        services.AddScoped<IScheduledReportRunStore, EfScheduledReportRunStore>();
        services.AddScoped<INotificationAttachmentSource, ReportRunAttachmentSource>();

        // HO-NTF-001 amendment (section 11.7 item 2): the deliver handler resolves an attachment by kind. Registered here, with the only source, so no other area's module changes.
        services.AddScoped<INotificationAttachmentResolver, NotificationAttachmentResolver>();
        services.AddScoped<INotificationSubjectGuard, ScheduledReportGuard>();

        // C2-C7. C8 (purge of expired report files) is not delivered: HO-XCT-001's retention sweep is not merged.
        services.AddScoped<ICommandHandlerAsync<CreateReportScheduleCommand>, CreateReportScheduleCommandHandler>();
        services.AddScoped<ICommandHandlerAsync<UpdateReportScheduleCommand>, UpdateReportScheduleCommandHandler>();
        services.AddScoped<ICommandHandlerAsync<PauseReportScheduleCommand>, PauseReportScheduleCommandHandler>();
        services.AddScoped<ICommandHandlerAsync<ResumeReportScheduleCommand>, ResumeReportScheduleCommandHandler>();
        services.AddScoped<ICommandHandlerAsync<CancelReportScheduleCommand>, CancelReportScheduleCommandHandler>();
        services.AddScoped<ICommandHandlerAsync<RecordScheduledReportRunCommand>, RecordScheduledReportRunCommandHandler>();
        services.AddScoped<ICommandHandlerAsync<RecordFailedReportRunCommand>, RecordFailedReportRunCommandHandler>();
        services.AddScoped<ICommandHandlerAsync<AdvanceReportScheduleCommand>, AdvanceReportScheduleCommandHandler>();

        services.AddScoped<IValidator<CreateReportScheduleCommand>, CreateReportScheduleCommandValidator>();
        services.AddScoped<IValidator<UpdateReportScheduleCommand>, UpdateReportScheduleCommandValidator>();
        services.AddScoped<IValidator<PauseReportScheduleCommand>, PauseReportScheduleCommandValidator>();
        services.AddScoped<IValidator<ResumeReportScheduleCommand>, ResumeReportScheduleCommandValidator>();
        services.AddScoped<IValidator<CancelReportScheduleCommand>, CancelReportScheduleCommandValidator>();
        services.AddScoped<IValidator<RecordScheduledReportRunCommand>, RecordScheduledReportRunCommandValidator>();
        services.AddScoped<IValidator<RecordFailedReportRunCommand>, RecordFailedReportRunCommandValidator>();
        services.AddScoped<IValidator<AdvanceReportScheduleCommand>, AdvanceReportScheduleCommandValidator>();

        services.AddScoped<IQueryHandlerAsync<GetReportSchedulesQuery, PagedResult<ReportScheduleListItemViewModel>>, GetReportSchedulesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetReportScheduleQuery, ReportScheduleDetailViewModel?>, GetReportScheduleQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetReportScheduleRunsQuery, PagedResult<ReportScheduleRunViewModel>?>, GetReportScheduleRunsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetEligibleReportRecipientsQuery, IReadOnlyList<EligibleReportRecipientViewModel>?>, GetEligibleReportRecipientsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<PreviewReportScheduleQuery, ReportSchedulePreviewOutcome>, PreviewReportScheduleQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDueReportSchedulesQuery, IReadOnlyList<DueReportScheduleViewModel>>, GetDueReportSchedulesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetReportSchedulePlanQuery, ReportSchedulePlanViewModel?>, GetReportSchedulePlanQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<CountFailedReportRunsQuery, int>, CountFailedReportRunsQueryHandler>();

        services.AddScoped<ScheduledReportRunner>();
        services.AddScoped<ReportingWorkCycle>();
        services.AddHostedService<ReportingWorker>();
    }

    private static void RegisterRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        // A second AddRateLimiter call is additive (ADR-341); UseRateLimiter() and the shared 429 body are the host's (Program.cs, HostPipelineExtensions). The numbers are read INSIDE the delegate
        // so a test factory's configuration is honoured however late it is added.
        services.AddRateLimiter(options =>
        {
            var reporting = configuration.GetSection(ReportingOptions.SectionName).Get<ReportingOptions>() ?? new ReportingOptions();
            var retryAfter = TimeSpan.FromSeconds(5);

            // ONE host-wide limiter shared by every report request: ADR-290's bounded concurrency is what protects interactive work from reports. It is created once and never disposed by a partition.
            var hostWide = new RetryAfterConcurrencyLimiter(
                new ConcurrencyLimiterOptions
                {
                    PermitLimit = Math.Max(1, reporting.MaxConcurrentReports),
                    QueueLimit = Math.Max(0, reporting.ConcurrencyQueueLimit),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                },
                retryAfter);

            ConcurrencyLimiter PerAccount(int permits) => new(new ConcurrencyLimiterOptions { PermitLimit = permits, QueueLimit = 0 });

            // Owner decision 2026-10-13: a per-account limit in front of the host-wide one, so one user cannot fill it and lock everyone else out.
            options.AddPolicy(ReportRateLimitPolicies.Reports, context =>
                RateLimitPartition.Get(AccountKey(context), _ => new ChainedLimiter([PerAccount(Math.Max(1, reporting.MaxConcurrentPerAccount))], [hostWide], retryAfter)));

            // run-now: five a window per account, one in flight per account, then the host-wide limit.
            options.AddPolicy(ReportRateLimitPolicies.ReportRunNow, context =>
                RateLimitPartition.Get(AccountKey(context), _ => new ChainedLimiter(
                    [PerAccount(1)],
                    [hostWide], retryAfter,
                    ownedLast: [new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 })])));

            // The dashboard polls every refresh interval: per-account only, so it never queues behind (or starves) the reports.
            options.AddPolicy(ReportRateLimitPolicies.Dashboard, context =>
                RateLimitPartition.Get(AccountKey(context), _ => new ChainedLimiter([PerAccount(Math.Max(1, reporting.MaxConcurrentPerAccount))], [], retryAfter)));
        });
    }

    /// <summary>The signed-in staff account (the host resolves the caller before the limiter runs), else the client address.</summary>
    private static string AccountKey(HttpContext context) =>
        context.Items[CatEcommerce.Infrastructure.Hosting.ResolvedCaller.HttpContextItemsKey] is CatEcommerce.Infrastructure.Hosting.ResolvedCaller { AccountId: { } id }
            ? "acct:" + id
            : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
}
