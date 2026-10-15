// Boarding module -- HO-BRD-001.
//
// Registers every command handler, validator (as IValidator<T> -- the platform resolves the interface, and a validator registered as its base class
// silently never runs), query handler, domain event handler, port and the worker this area needs. Handlers are registered explicitly; there is no
// assembly scanning. Program.cs already calls AddBoardingModule(...), so this file is the only one that changes per phase (ADR-341).

using CatEcommerce.Api.Background;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.CommandHandlers.Boarding;
using CatEcommerce.Application.DomainEventHandlers.Boarding;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Infrastructure.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using Platform.Infrastructure.Core.Events;
using RateLimitPartition = System.Threading.RateLimiting.RateLimitPartition;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Boarding;
using CatEcommerce.Infrastructure.Fakes.Boarding;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Read.QueryHandlers.Boarding;
using CatEcommerce.Read.ViewModels;
using FluentValidation;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Api.Modules;

public static class BoardingModule
{
    public static IServiceCollection AddBoardingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // ── Ports (HO-BRD-001 section 8.20) ──────────────────────────────────────────────────────────────────────────
        services.AddScoped<IBoardingReferenceReader, BoardingReferenceReader>();
        services.AddScoped<IBoardingUnitUsage, BoardingUnitUsage>();
        services.AddScoped<IBoardingInputs, BoardingInputs>();
        services.AddScoped<IBoardingDepositLedger, BillingDepositLedger>();
        services.AddScoped<IBoardingCareInputs, BoardingCareInputs>();
        services.AddScoped<IBoardingTaskFinder, BoardingTaskFinder>();
        services.AddScoped<IBoardingStayFinder, BoardingStayFinder>();

        services.AddOptions<BoardingOptions>().Bind(configuration.GetSection(BoardingOptions.SectionName)).ValidateOnStart();

        // The real implementations that replace the fail-closed fakes (docs/handover/CONTRACTS.md #12-#14): the billable source and the vet spending policy arrive with Phase G.
        services.AddScoped<IBoardingBillableSource, BoardingBillableSource>();
        services.AddScoped<IBoardingVetSpendingPolicy, BoardingVetSpendingPolicy>();
        services.AddScoped<IBoardingStayReferenceReader, BoardingStayReferenceReader>();
        services.AddScoped<IBoardingChargeLedger, BoardingChargeLedger>();
        services.AddSingleton<IUniqueViolationDetector, PostgresUniqueViolationDetector>();
        services.AddScoped<IFreshScopeExecutor, CatEcommerce.Infrastructure.Boarding.FreshScopeExecutor>();
        services.AddScoped<IBoardingWaitlistDaycareFinder, BoardingWaitlistDaycareFinder>();
        services.AddScoped<INotificationSubjectGuard, CatEcommerce.Infrastructure.Notifications.Guards.BoardingStayInHouseGuard>();

        // XCT: the boarding-media purge source (D-10) and the Boarding eraser (C-ALL-2).
        services.AddScoped<IRetentionPurgeSource, BoardingMediaPurgeSource>();
        services.AddScoped<IRetentionReviewSource, BoardingRetentionReviewSource>();
        services.AddScoped<IPersonalDataEraser, CatEcommerce.Infrastructure.Retention.Erasers.BoardingPersonalDataEraser>();

        RegisterCommands(services);
        RegisterQueries(services);
        RegisterDomainEventHandlers(services);

        // ── Worker (section 12.2, ADR-364) ───────────────────────────────────────────────────────────────────────────
        services.AddScoped<BoardingWorkCycle>();
        services.AddHostedService<BoardingWorker>();

        // ── Rate limiting (section 12.3) -- limits read from BoardingOptions at request time, so a test factory can raise them ───────────
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(BoardingRateLimitPolicies.Availability, context =>
            {
                var limits = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<BoardingOptions>>().Value.Availability;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = limits.PermitLimit, Window = TimeSpan.FromMinutes(limits.WindowMinutes), QueueLimit = 0 });
            });

            options.AddPolicy(BoardingRateLimitPolicies.Booking, context =>
            {
                var limits = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<BoardingOptions>>().Value.Booking;
                var caller = context.Items[CatEcommerce.Infrastructure.Hosting.ResolvedCaller.HttpContextItemsKey] as CatEcommerce.Infrastructure.Hosting.ResolvedCaller;
                var key = caller is { Kind: CatEcommerce.Infrastructure.Hosting.ResolvedCallerKind.Customer }
                    ? $"customer:{caller.AccountId}" : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
                return RateLimitPartition.GetFixedWindowLimiter(
                    key, _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = limits.PermitLimit, Window = TimeSpan.FromMinutes(limits.WindowMinutes), QueueLimit = 0 });
            });
        });

        return services;
    }

    private static void RegisterCommands(IServiceCollection services)
    {
        // Phase H -- waitlist, daycare, daily update
        services.AddScoped<ICommandHandlerAsync<JoinBoardingWaitlistCommand>, JoinBoardingWaitlistCommandHandler>();
        services.AddScoped<IValidator<JoinBoardingWaitlistCommand>, JoinBoardingWaitlistCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<WithdrawBoardingWaitlistCommand>, WithdrawBoardingWaitlistCommandHandler>();
        services.AddScoped<IValidator<WithdrawBoardingWaitlistCommand>, WithdrawBoardingWaitlistCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<NotifyWaitlistEntryCommand>, NotifyWaitlistEntryCommandHandler>();
        services.AddScoped<IValidator<NotifyWaitlistEntryCommand>, NotifyWaitlistEntryCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CloseStaleBoardingWaitlistCommand>, CloseStaleBoardingWaitlistCommandHandler>();
        services.AddScoped<IValidator<CloseStaleBoardingWaitlistCommand>, CloseStaleBoardingWaitlistCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SetDaycareRateCardCommand>, SetDaycareRateCardCommandHandler>();
        services.AddScoped<IValidator<SetDaycareRateCardCommand>, SetDaycareRateCardCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<BookDaycareVisitCommand>, BookDaycareVisitCommandHandler>();
        services.AddScoped<IValidator<BookDaycareVisitCommand>, BookDaycareVisitCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CheckInDaycareVisitCommand>, CheckInDaycareVisitCommandHandler>();
        services.AddScoped<IValidator<CheckInDaycareVisitCommand>, CheckInDaycareVisitCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CheckOutDaycareVisitCommand>, CheckOutDaycareVisitCommandHandler>();
        services.AddScoped<IValidator<CheckOutDaycareVisitCommand>, CheckOutDaycareVisitCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CancelDaycareVisitCommand>, CancelDaycareVisitCommandHandler>();
        services.AddScoped<IValidator<CancelDaycareVisitCommand>, CancelDaycareVisitCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordDaycareNoShowCommand>, RecordDaycareNoShowCommandHandler>();
        services.AddScoped<IValidator<RecordDaycareNoShowCommand>, RecordDaycareNoShowCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SendBoardingDailyUpdateCommand>, SendBoardingDailyUpdateCommandHandler>();
        services.AddScoped<IValidator<SendBoardingDailyUpdateCommand>, SendBoardingDailyUpdateCommandValidator>();

        // Phase G -- referral, vet approvals, extension, relocation, check-out, attention, cycle invoices
        services.AddScoped<ICommandHandlerAsync<ReferStayCatToClinicCommand>, ReferStayCatToClinicCommandHandler>();
        services.AddScoped<IValidator<ReferStayCatToClinicCommand>, ReferStayCatToClinicCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordReferralReturnCommand>, RecordReferralReturnCommandHandler>();
        services.AddScoped<IValidator<RecordReferralReturnCommand>, RecordReferralReturnCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RequestVetChargeApprovalCommand>, RequestVetChargeApprovalCommandHandler>();
        services.AddScoped<IValidator<RequestVetChargeApprovalCommand>, RequestVetChargeApprovalCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RespondToVetChargeApprovalCommand>, RespondToVetChargeApprovalCommandHandler>();
        services.AddScoped<IValidator<RespondToVetChargeApprovalCommand>, RespondToVetChargeApprovalCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordVetChargeApprovalByStaffCommand>, RecordVetChargeApprovalByStaffCommandHandler>();
        services.AddScoped<IValidator<RecordVetChargeApprovalByStaffCommand>, RecordVetChargeApprovalByStaffCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ExtendBoardingStayCommand>, ExtendBoardingStayCommandHandler>();
        services.AddScoped<IValidator<ExtendBoardingStayCommand>, ExtendBoardingStayCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RelocateBoardingStayCommand>, RelocateBoardingStayCommandHandler>();
        services.AddScoped<IValidator<RelocateBoardingStayCommand>, RelocateBoardingStayCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CheckOutBoardingStayCommand>, CheckOutBoardingStayCommandHandler>();
        services.AddScoped<IValidator<CheckOutBoardingStayCommand>, CheckOutBoardingStayCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ClearStayAttentionCommand>, ClearStayAttentionCommandHandler>();
        services.AddScoped<IValidator<ClearStayAttentionCommand>, ClearStayAttentionCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<InvoiceBoardingCycleCommand>, InvoiceBoardingCycleCommandHandler>();
        services.AddScoped<IValidator<InvoiceBoardingCycleCommand>, InvoiceBoardingCycleCommandValidator>();

        // Phase F -- media and incidents
        services.AddScoped<ICommandHandlerAsync<AddStayMediaCommand>, AddStayMediaCommandHandler>();
        services.AddScoped<IValidator<AddStayMediaCommand>, AddStayMediaCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<HideStayMediaCommand>, HideStayMediaCommandHandler>();
        services.AddScoped<IValidator<HideStayMediaCommand>, HideStayMediaCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordIncidentCommand>, RecordIncidentCommandHandler>();
        services.AddScoped<IValidator<RecordIncidentCommand>, RecordIncidentCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<AcknowledgeIncidentCommand>, AcknowledgeIncidentCommandHandler>();
        services.AddScoped<IValidator<AcknowledgeIncidentCommand>, AcknowledgeIncidentCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordIncidentAcknowledgementCommand>, RecordIncidentAcknowledgementCommandHandler>();
        services.AddScoped<IValidator<RecordIncidentAcknowledgementCommand>, RecordIncidentAcknowledgementCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<EscalateIncidentCommand>, EscalateIncidentCommandHandler>();
        services.AddScoped<IValidator<EscalateIncidentCommand>, EscalateIncidentCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordIncidentPhoneCallCommand>, RecordIncidentPhoneCallCommandHandler>();
        services.AddScoped<IValidator<RecordIncidentPhoneCallCommand>, RecordIncidentPhoneCallCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ResolveIncidentCommand>, ResolveIncidentCommandHandler>();
        services.AddScoped<IValidator<ResolveIncidentCommand>, ResolveIncidentCommandValidator>();

        // Phase E -- check-in, care tasks, observations, isolation
        services.AddScoped<ICommandHandlerAsync<CheckInBoardingStayCommand>, CheckInBoardingStayCommandHandler>();
        services.AddScoped<IValidator<CheckInBoardingStayCommand>, CheckInBoardingStayCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<GenerateCareTasksCommand>, GenerateCareTasksCommandHandler>();
        services.AddScoped<IValidator<GenerateCareTasksCommand>, GenerateCareTasksCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CompleteCareTaskCommand>, CompleteCareTaskCommandHandler>();
        services.AddScoped<IValidator<CompleteCareTaskCommand>, CompleteCareTaskCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SkipCareTaskCommand>, SkipCareTaskCommandHandler>();
        services.AddScoped<IValidator<SkipCareTaskCommand>, SkipCareTaskCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordCareObservationCommand>, RecordCareObservationCommandHandler>();
        services.AddScoped<IValidator<RecordCareObservationCommand>, RecordCareObservationCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<FlagSuspectedInfectiousCommand>, FlagSuspectedInfectiousCommandHandler>();
        services.AddScoped<IValidator<FlagSuspectedInfectiousCommand>, FlagSuspectedInfectiousCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ClearSuspectedInfectiousCommand>, ClearSuspectedInfectiousCommandHandler>();
        services.AddScoped<IValidator<ClearSuspectedInfectiousCommand>, ClearSuspectedInfectiousCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordIsolationEntryCommand>, RecordIsolationEntryCommandHandler>();
        services.AddScoped<IValidator<RecordIsolationEntryCommand>, RecordIsolationEntryCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordIsolationExitCommand>, RecordIsolationExitCommandHandler>();
        services.AddScoped<IValidator<RecordIsolationExitCommand>, RecordIsolationExitCommandValidator>();

        // Phase D -- confirm, lapse, expire, cancel, no-show
        services.AddScoped<ICommandHandlerAsync<ConfirmBoardingStayCommand>, ConfirmBoardingStayCommandHandler>();
        services.AddScoped<IValidator<ConfirmBoardingStayCommand>, ConfirmBoardingStayCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<LapseBoardingReservationCommand>, LapseBoardingReservationCommandHandler>();
        services.AddScoped<IValidator<LapseBoardingReservationCommand>, LapseBoardingReservationCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ExpireBoardingEnquiryCommand>, ExpireBoardingEnquiryCommandHandler>();
        services.AddScoped<IValidator<ExpireBoardingEnquiryCommand>, ExpireBoardingEnquiryCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CancelBoardingStayCommand>, CancelBoardingStayCommandHandler>();
        services.AddScoped<IValidator<CancelBoardingStayCommand>, CancelBoardingStayCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordBoardingNoShowCommand>, RecordBoardingNoShowCommandHandler>();
        services.AddScoped<IValidator<RecordBoardingNoShowCommand>, RecordBoardingNoShowCommandValidator>();

        // Phase C -- availability, enquiry, gate, care profile, reserve
        services.AddScoped<ICommandHandlerAsync<CreateBoardingEnquiryCommand>, CreateBoardingEnquiryCommandHandler>();
        services.AddScoped<IValidator<CreateBoardingEnquiryCommand>, CreateBoardingEnquiryCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ChangeBoardingEnquiryCommand>, ChangeBoardingEnquiryCommandHandler>();
        services.AddScoped<IValidator<ChangeBoardingEnquiryCommand>, ChangeBoardingEnquiryCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SetStayCareProfileCommand>, SetStayCareProfileCommandHandler>();
        services.AddScoped<IValidator<SetStayCareProfileCommand>, SetStayCareProfileCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<OverrideVaccinationGateCommand>, OverrideVaccinationGateCommandHandler>();
        services.AddScoped<IValidator<OverrideVaccinationGateCommand>, OverrideVaccinationGateCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ReserveBoardingStayCommand>, ReserveBoardingStayCommandHandler>();
        services.AddScoped<IValidator<ReserveBoardingStayCommand>, ReserveBoardingStayCommandValidator>();

        // Phase B -- units, maintenance, rate cards, care routine
        services.AddScoped<ICommandHandlerAsync<CreateBoardingUnitCommand>, CreateBoardingUnitCommandHandler>();
        services.AddScoped<IValidator<CreateBoardingUnitCommand>, CreateBoardingUnitCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<UpdateBoardingUnitCommand>, UpdateBoardingUnitCommandHandler>();
        services.AddScoped<IValidator<UpdateBoardingUnitCommand>, UpdateBoardingUnitCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SetBoardingUnitActivationCommand>, SetBoardingUnitActivationCommandHandler>();
        services.AddScoped<IValidator<SetBoardingUnitActivationCommand>, SetBoardingUnitActivationCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CreateMaintenanceBlockCommand>, CreateMaintenanceBlockCommandHandler>();
        services.AddScoped<IValidator<CreateMaintenanceBlockCommand>, CreateMaintenanceBlockCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CancelMaintenanceBlockCommand>, CancelMaintenanceBlockCommandHandler>();
        services.AddScoped<IValidator<CancelMaintenanceBlockCommand>, CancelMaintenanceBlockCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SetBoardingRateCardCommand>, SetBoardingRateCardCommandHandler>();
        services.AddScoped<IValidator<SetBoardingRateCardCommand>, SetBoardingRateCardCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SetBoardingCareRoutineCommand>, SetBoardingCareRoutineCommandHandler>();
        services.AddScoped<IValidator<SetBoardingCareRoutineCommand>, SetBoardingCareRoutineCommandValidator>();
    }

    private static void RegisterDomainEventHandlers(IServiceCollection services)
    {
        services.AddScoped<IEventHandlerAsync<VaccinationGateOverriddenEvent>, AuditVaccinationOverrideHandler>();
        services.AddScoped<IEventHandlerAsync<StayVetAuthorityChangedEvent>, AuditVetAuthorityChangeHandler>();
        services.AddScoped<IEventHandlerAsync<StayCatCheckedInEvent>, RecordCatWeightOnBoardingHandler>();
        services.AddScoped<IEventHandlerAsync<StayCatCheckedOutEvent>, RecordCatWeightOnBoardingHandler>();
        services.AddScoped<IEventHandlerAsync<StayCareProfileSetEvent>, VoidPendingTasksOnCareProfileChangedHandler>();
        services.AddScoped<IEventHandlerAsync<BoardingStayCancelledEvent>, VoidTasksOnStayEndedHandler>();
        services.AddScoped<IEventHandlerAsync<BoardingStayCheckedOutEvent>, VoidTasksOnStayEndedHandler>();
        services.AddScoped<IEventHandlerAsync<BoardingStayCheckedOutEvent>, FlagStaysOnOverstayHandler>();
        services.AddScoped<IEventHandlerAsync<ReferralVisitOpenedEvent>, LinkReferralVisitHandler>();
        services.AddScoped<IEventHandlerAsync<BoardingVetChargeRecordedEvent>, RecordVetCommitmentHandler>();
        services.AddScoped<IEventHandlerAsync<CatEcommerce.Domain.Events.Surgery.SurgeryScheduledEvent>, RecordSurgeryOnBoardedCatHandler>();
        services.AddScoped<IEventHandlerAsync<CatEcommerce.Domain.Events.Surgery.SurgeryRescheduledEvent>, RecordSurgeryOnBoardedCatHandler>();
        services.AddScoped<IEventHandlerAsync<CatEcommerce.Domain.Events.Surgery.SurgeryDischargedEvent>, DischargeSurgeryOnBoardedCatHandler>();
        services.AddScoped<IEventHandlerAsync<CatReturnedFromClinicEvent>, SkipCareTasksWhileAwayHandler>();
        services.AddScoped<IEventHandlerAsync<StaySurgeryDischargedEvent>, SkipCareTasksWhileAwayHandler>();
        services.AddScoped<IEventHandlerAsync<CatEcommerce.Domain.Events.Administration.StaffLeaveApprovedEvent>, ReassignCareTasksOnLeaveApprovedHandler>();
        services.AddScoped<IEventHandlerAsync<CatEcommerce.Domain.Events.Cat.CatMarkedDeceasedEvent>, FlagStaysOnCatDeceasedHandler>();
        services.AddScoped<IEventHandlerAsync<CatEcommerce.Domain.Events.Cat.CatOwnershipTransferredEvent>, FlagStaysOnCatTransferredHandler>();
        services.AddScoped<IEventHandlerAsync<CatEcommerce.Domain.Events.Account.CustomerMergedEvent>, ReassignStaysOnCustomerMergedHandler>();
        services.AddScoped<IEventHandlerAsync<CatEcommerce.Domain.Events.Billing.PaymentRejectedEvent>, ApplyBillingDepositOutcomeHandler>();
    }

    private static void RegisterQueries(IServiceCollection services)
    {
        services.AddScoped<IQueryHandlerAsync<GetCareTaskDayListQuery, CareTaskDayListViewModel>, GetCareTaskDayListQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStayObservationsQuery, StayObservationsViewModel?>, GetStayObservationsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaysInHouseQuery, IReadOnlyList<InHouseStayViewModel>>, GetStaysInHouseQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetReservationsReadyToConfirmQuery, IReadOnlyList<Guid>>, GetReservationsReadyToConfirmQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetExpiredReservationsQuery, IReadOnlyList<Guid>>, GetExpiredReservationsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPastEnquiriesQuery, IReadOnlyList<Guid>>, GetPastEnquiriesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBoardingAvailabilityQuery, BoardingAvailabilityViewModel>, GetBoardingAvailabilityQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStayDetailQuery, StayViewModel?>, GetStayDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerStaysQuery, PagedResult<StaySummaryViewModel>>, GetCustomerStaysQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaysQuery, PagedResult<StaySummaryViewModel>>, GetStaysQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBoardingOccupancyQuery, BoardingOccupancyViewModel>, GetBoardingOccupancyQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBoardingUnitsQuery, IReadOnlyList<BoardingUnitViewModel>>, GetBoardingUnitsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetUnitMaintenanceQuery, IReadOnlyList<MaintenanceBlockViewModel>>, GetUnitMaintenanceQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBoardingRateCardsQuery, IReadOnlyList<BoardingRateCardViewModel>>, GetBoardingRateCardsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBoardingCareRoutineQuery, BoardingCareRoutineViewModel>, GetBoardingCareRoutineQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetEarlyCheckoutPreviewQuery, EarlyCheckoutPreviewViewModel?>, GetEarlyCheckoutPreviewQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerWaitlistQuery, IReadOnlyList<BoardingWaitlistEntryViewModel>>, GetCustomerWaitlistQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDaycareDayQuery, DaycareDayViewModel>, GetDaycareDayQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerDaycareQuery, IReadOnlyList<DaycareVisitViewModel>>, GetCustomerDaycareQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDaycareRateCardsQuery, IReadOnlyList<DaycareRateCardViewModel>>, GetDaycareRateCardsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetWaitlistCandidatesQuery, IReadOnlyList<Guid>>, GetWaitlistCandidatesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaleWaitlistEntriesQuery, IReadOnlyList<Guid>>, GetStaleWaitlistEntriesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDailyUpdateCandidatesQuery, IReadOnlyList<DailyUpdateCandidateViewModel>>, GetDailyUpdateCandidatesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaySummaryQuery, StayReportViewModel?>, GetStaySummaryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetVetHeadroomQuery, IReadOnlyList<CatHeadroomViewModel>?>, GetVetHeadroomQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRelocationOptionsQuery, IReadOnlyList<RelocationOptionViewModel>?>, GetRelocationOptionsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaysDueForInvoicingQuery, IReadOnlyList<StayInvoiceDueViewModel>>, GetStaysDueForInvoicingQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetIncidentsQuery, IReadOnlyList<IncidentViewModel>>, GetIncidentsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerIncidentQuery, IncidentViewModel?>, GetCustomerIncidentQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStayDiaryQuery, StayDiaryViewModel?>, GetStayDiaryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStayMediaQuery, IReadOnlyList<DiaryMediaViewModel>?>, GetStayMediaQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetIncidentsToEscalateQuery, IReadOnlyList<Guid>>, GetIncidentsToEscalateQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBoardingDashboardQuery, BoardingDashboardViewModel>, GetBoardingDashboardQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBoardingUnitTypesQuery, IReadOnlyList<BoardingUnitTypeViewModel>>, GetBoardingUnitTypesQueryHandler>();
    }
}
