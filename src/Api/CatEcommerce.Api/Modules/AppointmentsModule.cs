// Appointments module -- HO-APT-001.
//
// Registers every command handler, validator, query handler, domain event handler and domain
// service this area needs, plus the Clinic worker (ClinicWorker/ClinicWorkCycle, §12.2) and its own
// rate-limit policies (ADR-341). Program.cs already calls AddAppointmentsModule(builder.Configuration)
// -- this file is the only one that needed to change.

using CatEcommerce.Api.Background;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.CommandHandlers.Clinic;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Clinic;
using CatEcommerce.Application.DomainEventHandlers.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Events.Cat;
using CatEcommerce.Domain.Events.Clinic;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Infrastructure.Notifications.Guards;
using CatEcommerce.Infrastructure.Scheduling;
using CatEcommerce.Read.Queries.Clinic;
using CatEcommerce.Read.QueryHandlers.Clinic;
using CatEcommerce.Read.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Events;
using Platform.Infrastructure.Core.Queries;
using RateLimitPartition = System.Threading.RateLimiting.RateLimitPartition;

namespace CatEcommerce.Api.Modules;

public static class AppointmentsModule
{
    public static IServiceCollection AddAppointmentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ClinicOptions>()
            .Bind(configuration.GetSection(ClinicOptions.SectionName))
            .ValidateOnStart();

        // ── §8.12 ports ───────────────────────────────────────────────────────────────────────────
        services.AddScoped<IAppointmentSchedulingInputs, AppointmentSchedulingInputs>();
        services.AddScoped<IStaleHoldReleaser, StaleHoldReleaser>();
        services.AddScoped<IFlaggableAppointmentFinder, FlaggableAppointmentFinder>();
        services.AddScoped<IWaitlistEntryFinder, WaitlistEntryFinder>();

        // SECURITY gate fix (2026-09-28, HIGH): serialises PlaceHold per customer behind a Postgres
        // transaction-scoped advisory lock -- see ICustomerHoldSerializer's own doc comment.
        services.AddScoped<ICustomerHoldSerializer, CustomerHoldSerializer>();

        // HO-NTF-001's SubjectGuardResolver resolves IEnumerable<INotificationSubjectGuard> -- this
        // adds the AppointmentReminder guard alongside Notifications' own two, additively.
        services.AddScoped<INotificationSubjectGuard, AppointmentReminderGuard>();

        // HO-XCT-001 §8.8/OQ-7: Appointments is exempted from erasure, not given an eraser --
        // ReasonForVisit/PresentingComplaint are part of the clinical record and retained under
        // D-8 (see ErasureExemptions.Appointments.cs). No IPersonalDataEraser is registered here.

        RegisterCommands(services);
        RegisterQueries(services);
        RegisterDomainEventHandlers(services);

        // ── Worker (§12.2) ───────────────────────────────────────────────────────────────────────
        services.AddScoped<ClinicWorkCycle>();
        services.AddHostedService<ClinicWorker>();

        // ── Rate limiting (§12.3/§12.7) -- additive AddRateLimiter call, per ADR-341. Reads the
        // permit limits from ClinicOptions (not a literal), so a test factory can raise/lower them
        // the same way AccountsApiFactory does for its own policies. ─────────────────────────────
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(AppointmentRateLimitPolicies.Availability, context =>
            {
                var limits = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClinicOptions>>().Value.Availability;
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    { PermitLimit = limits.PermitLimit, Window = TimeSpan.FromMinutes(limits.WindowMinutes), QueueLimit = 0 });
            });

            // SECURITY gate fix (2026-09-28, HIGH): partitioned by IP only, one account on one IP
            // (or rotating IPs) was throttled no harder than any other caller. Partitioned by the
            // signed-in customer id when one is resolved instead -- the same
            // ResolvedCaller/HttpContext.Items pattern AccountsModule's ProfileUpdate policy and
            // FileStorageModule's FileUpload policy already use -- falling back to IP only when no
            // session is resolved (PlaceHold requires [RequireCustomerSession], so an IP fallback
            // here is only ever hit for an already-401 request; the rate limiter still runs ahead
            // of the controller's own auth check). Configured limits are unchanged.
            options.AddPolicy(AppointmentRateLimitPolicies.Booking, context =>
                CustomerPartitionedLimiter(context, o => o.Booking));

            // Backlog #27: reschedule/confirm/cancel/waitlist join -- same per-customer partitioning,
            // own counter so a change never eats into the hold budget.
            options.AddPolicy(AppointmentRateLimitPolicies.Change, context =>
                CustomerPartitionedLimiter(context, o => o.Change));

            // Backlog #28: the public performer lookup, per IP, own counter.
            options.AddPolicy(AppointmentRateLimitPolicies.Performers, context =>
            {
                var limits = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClinicOptions>>().Value.Performers;
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    { PermitLimit = limits.PermitLimit, Window = TimeSpan.FromMinutes(limits.WindowMinutes), QueueLimit = 0 });
            });
        });

        return services;
    }

    /// <summary>Partitioned by the signed-in customer id when one is resolved, by IP otherwise.</summary>
    private static System.Threading.RateLimiting.RateLimitPartition<string> CustomerPartitionedLimiter(
        Microsoft.AspNetCore.Http.HttpContext context, Func<ClinicOptions, RateLimitPolicyOptions> select)
    {
        var limits = select(context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClinicOptions>>().Value);
        var caller = context.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller;
        var partitionKey = caller is { Kind: ResolvedCallerKind.Customer }
            ? $"customer:{caller.AccountId}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            { PermitLimit = limits.PermitLimit, Window = TimeSpan.FromMinutes(limits.WindowMinutes), QueueLimit = 0 });
    }

    private static void RegisterCommands(IServiceCollection services)
    {
        services.AddScoped<ICommandHandlerAsync<PlaceSlotHoldCommand>, PlaceSlotHoldCommandHandler>();
        services.AddScoped<IValidator<PlaceSlotHoldCommand>, PlaceSlotHoldCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ReleaseSlotHoldCommand>, ReleaseSlotHoldCommandHandler>();
        services.AddScoped<IValidator<ReleaseSlotHoldCommand>, ReleaseSlotHoldCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ConfirmAppointmentCommand>, ConfirmAppointmentCommandHandler>();
        services.AddScoped<IValidator<ConfirmAppointmentCommand>, ConfirmAppointmentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RescheduleAppointmentCommand>, RescheduleAppointmentCommandHandler>();
        services.AddScoped<IValidator<RescheduleAppointmentCommand>, RescheduleAppointmentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CancelAppointmentCommand>, CancelAppointmentCommandHandler>();
        services.AddScoped<IValidator<CancelAppointmentCommand>, CancelAppointmentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<BookAppointmentByStaffCommand>, BookAppointmentByStaffCommandHandler>();
        services.AddScoped<IValidator<BookAppointmentByStaffCommand>, BookAppointmentByStaffCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<MoveAppointmentCommand>, MoveAppointmentCommandHandler>();
        services.AddScoped<IValidator<MoveAppointmentCommand>, MoveAppointmentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RegisterWalkInCommand>, RegisterWalkInCommandHandler>();
        services.AddScoped<IValidator<RegisterWalkInCommand>, RegisterWalkInCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CheckInAppointmentCommand>, CheckInAppointmentCommandHandler>();
        services.AddScoped<IValidator<CheckInAppointmentCommand>, CheckInAppointmentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetTriagePriorityCommand>, SetTriagePriorityCommandHandler>();
        services.AddScoped<IValidator<SetTriagePriorityCommand>, SetTriagePriorityCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<StartAppointmentCommand>, StartAppointmentCommandHandler>();
        services.AddScoped<IValidator<StartAppointmentCommand>, StartAppointmentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CompleteAppointmentCommand>, CompleteAppointmentCommandHandler>();
        services.AddScoped<IValidator<CompleteAppointmentCommand>, CompleteAppointmentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordNoShowCommand>, RecordNoShowCommandHandler>();
        services.AddScoped<IValidator<RecordNoShowCommand>, RecordNoShowCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<JoinWaitlistCommand>, JoinWaitlistCommandHandler>();
        services.AddScoped<IValidator<JoinWaitlistCommand>, JoinWaitlistCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<WithdrawFromWaitlistCommand>, WithdrawFromWaitlistCommandHandler>();
        services.AddScoped<IValidator<WithdrawFromWaitlistCommand>, WithdrawFromWaitlistCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<OfferWaitlistSlotCommand>, OfferWaitlistSlotCommandHandler>();
        services.AddScoped<IValidator<OfferWaitlistSlotCommand>, OfferWaitlistSlotCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<LapseWaitlistOfferCommand>, LapseWaitlistOfferCommandHandler>();
        services.AddScoped<IValidator<LapseWaitlistOfferCommand>, LapseWaitlistOfferCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CloseStaleWaitlistEntriesCommand>, CloseStaleWaitlistEntriesCommandHandler>();
        services.AddScoped<IValidator<CloseStaleWaitlistEntriesCommand>, CloseStaleWaitlistEntriesCommandValidator>();
    }

    private static void RegisterQueries(IServiceCollection services)
    {
        services.AddScoped<IQueryHandlerAsync<GetAvailableSlotsQuery, AvailableSlotsViewModel>, GetAvailableSlotsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaffAvailableSlotsQuery, IReadOnlyList<StaffSlotViewModel>>, GetStaffAvailableSlotsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBookingPerformersQuery, IReadOnlyList<BookingPerformerViewModel>>, GetBookingPerformersQueryHandler>();

        services.AddScoped<IQueryHandlerAsync<GetCustomerAppointmentsQuery, PagedResult<AppointmentSummaryViewModel>>, GetCustomerAppointmentsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerAppointmentDetailQuery, CustomerAppointmentDetailViewModel?>, GetCustomerAppointmentDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerAppointmentSummaryQuery, CustomerAppointmentSummaryViewModel>, GetCustomerAppointmentSummaryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerWaitlistQuery, IReadOnlyList<WaitlistEntryViewModel>>, GetCustomerWaitlistQueryHandler>();

        services.AddScoped<IQueryHandlerAsync<GetAppointmentDetailQuery, AppointmentDetailViewModel?>, GetAppointmentDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDayListQuery, IReadOnlyList<CalendarAppointmentViewModel>>, GetDayListQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCalendarQuery, CalendarViewModel>, GetCalendarQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetClinicQueueQuery, ClinicQueueViewModel>, GetClinicQueueQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetFlaggedAppointmentsQuery, IReadOnlyList<FlaggedAppointmentViewModel>>, GetFlaggedAppointmentsQueryHandler>();

        services.AddScoped<IQueryHandlerAsync<GetWaitlistOfferCandidatesQuery, IReadOnlyList<Guid>>, GetWaitlistOfferCandidatesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetLapsedWaitlistOffersQuery, IReadOnlyList<Guid>>, GetLapsedWaitlistOffersQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPastWaitlistEntriesQuery, IReadOnlyList<Guid>>, GetPastWaitlistEntriesQueryHandler>();
    }

    private static void RegisterDomainEventHandlers(IServiceCollection services)
    {
        services.AddScoped<IEventHandlerAsync<StaffLeaveApprovedEvent>, FlagAppointmentsOnLeaveApprovedHandler>();
        services.AddScoped<IEventHandlerAsync<LeaveCancelledEvent>, UnflagAppointmentsOnLeaveCancelledHandler>();
        services.AddScoped<IEventHandlerAsync<BranchClosureCreatedEvent>, FlagAppointmentsOnClosureHandler>();
        services.AddScoped<IEventHandlerAsync<BranchClosureCancelledEvent>, UnflagAppointmentsOnClosureCancelledHandler>();
        services.AddScoped<IEventHandlerAsync<CatMarkedDeceasedEvent>, FlagAppointmentsOnCatDeceasedHandler>();
        services.AddScoped<IEventHandlerAsync<CatOwnershipTransferredEvent>, FlagAppointmentsOnCatTransferredHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerMergedEvent>, ReassignAppointmentsOnCustomerMergedHandler>();
        services.AddScoped<IEventHandlerAsync<AppointmentNoShowRecordedEvent>, AuditNoShowHandler>();
    }
}
