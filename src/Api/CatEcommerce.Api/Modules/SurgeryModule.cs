// Surgery module -- HO-SRG-001.
//
// Registers every command handler, validator (as IValidator<T>, never the AbstractValidator base), query handler, domain event handler and port this area
// needs (ADR-341). Program.cs already calls AddSurgeryModule(builder.Configuration) -- this file is the only one that needed to change. The ports owned by other
// areas (IStockLedger, IControlledDrugRegister, IStaffWitnessVerifier, IFormularyReader, INotificationOutbox, IBusinessSettings) are registered by their owners;
// the Surgery-owned ports (clock, finder, consent reader, scheduling inputs, appointment enlister, campaign pricing) are registered here.

using CatEcommerce.Application.CommandHandlers.Surgery;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Application.DomainEventHandlers.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Events.Cat;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Infrastructure.Notifications.Guards;
using CatEcommerce.Infrastructure.Scheduling;
using CatEcommerce.Infrastructure.Surgery;
using CatEcommerce.Read.Queries.Surgery;
using CatEcommerce.Read.QueryHandlers.Surgery;
using CatEcommerce.Read.ViewModels;
using FluentValidation;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Events;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Api.Modules;

public static class SurgeryModule
{
    public static IServiceCollection AddSurgeryModule(this IServiceCollection services, IConfiguration configuration)
    {
        RegisterPorts(services);
        RegisterProtocols(services);
        RegisterScheduling(services);
        RegisterChecklistAndStart(services);
        RegisterAnaesthesiaLog(services);
        RegisterReportAndNeuterStatus(services);
        RegisterRecoveryAndDischarge(services);
        RegisterCampaigns(services);

        return services;
    }

    private static void RegisterPorts(IServiceCollection services)
    {
        services.AddScoped<ISurgerySchedulingInputs, SurgerySchedulingInputs>();
        services.AddScoped<ISurgeryAppointmentEnlister, SurgeryAppointmentEnlister>();
        services.AddScoped<ISurgeryClock, SurgeryClock>();
        services.AddScoped<ISurgeryFinder, SurgeryFinder>();
        services.AddScoped<ISurgeryConsentReader, SurgeryConsentReader>();
        services.AddScoped<ISurgeryCampaignPricing, SurgeryCampaignPricing>();

        // C-ALL-3 -- clinical records past retention are flagged for review (never deleted).
        services.AddScoped<IRetentionReviewSource, SurgeryRetentionReviewSource>();

        // HO-NTF-001's SubjectGuardResolver resolves IEnumerable<INotificationSubjectGuard> -- these add the two Surgery guards alongside the others, additively.
        services.AddScoped<INotificationSubjectGuard, SurgeryPreOpGuard>();
        services.AddScoped<INotificationSubjectGuard, SurgeryFollowUpGuard>();

        // HO-XCT-001 section 8.8/OQ-7: Surgery is exempted from erasure, not given an eraser (see ErasureExemptions.Surgery.cs). No IPersonalDataEraser is registered here.
    }

    private static void RegisterProtocols(IServiceCollection services)
    {
        services.AddScoped<ICommandHandlerAsync<UpsertSurgeryProtocolCommand>, UpsertSurgeryProtocolCommandHandler>();
        services.AddScoped<IValidator<UpsertSurgeryProtocolCommand>, UpsertSurgeryProtocolCommandValidator>();

        services.AddScoped<IQueryHandlerAsync<GetSurgeryProtocolsQuery, IReadOnlyList<SurgeryProtocolViewModel>>, GetSurgeryProtocolsQueryHandler>();
    }

    private static void RegisterScheduling(IServiceCollection services)
    {
        services.AddScoped<ICommandHandlerAsync<BookSurgeryCommand>, BookSurgeryCommandHandler>();
        services.AddScoped<IValidator<BookSurgeryCommand>, BookSurgeryCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RescheduleSurgeryCommand>, RescheduleSurgeryCommandHandler>();
        services.AddScoped<IValidator<RescheduleSurgeryCommand>, RescheduleSurgeryCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CancelSurgeryCommand>, CancelSurgeryCommandHandler>();
        services.AddScoped<IValidator<CancelSurgeryCommand>, CancelSurgeryCommandValidator>();

        services.AddScoped<IQueryHandlerAsync<GetSurgeryServicesQuery, IReadOnlyList<SurgeryServiceViewModel>>, GetSurgeryServicesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetSurgeryStaffQuery, SurgeryStaffViewModel>, GetSurgeryStaffQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetSurgeryAvailabilityQuery, SurgeryAvailabilityViewModel>, GetSurgeryAvailabilityQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetSurgeryDayListQuery, SurgeryDayListViewModel>, GetSurgeryDayListQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetSurgeryListQuery, PagedResult<SurgerySummaryViewModel>>, GetSurgeryListQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetSurgeryDetailQuery, SurgeryDetailViewModel?>, GetSurgeryDetailQueryHandler>();

        services.AddScoped<IEventHandlerAsync<CustomerMergedEvent>, ReassignSurgeriesOnCustomerMergedHandler>();
        services.AddScoped<IEventHandlerAsync<CatOwnershipTransferredEvent>, FlagSurgeryOnCatTransferredHandler>();
    }

    private static void RegisterReportAndNeuterStatus(IServiceCollection services)
    {
        services.AddScoped<ICommandHandlerAsync<EndSurgicalProcedureCommand>, EndSurgicalProcedureCommandHandler>();
        services.AddScoped<IValidator<EndSurgicalProcedureCommand>, EndSurgicalProcedureCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SaveSurgicalReportDraftCommand>, SaveSurgicalReportDraftCommandHandler>();
        services.AddScoped<IValidator<SaveSurgicalReportDraftCommand>, SaveSurgicalReportDraftCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<FinaliseSurgicalReportCommand>, FinaliseSurgicalReportCommandHandler>();
        services.AddScoped<IValidator<FinaliseSurgicalReportCommand>, FinaliseSurgicalReportCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<AddSurgicalReportAddendumCommand>, AddSurgicalReportAddendumCommandHandler>();
        services.AddScoped<IValidator<AddSurgicalReportAddendumCommand>, AddSurgicalReportAddendumCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ApplySterilisationOutcomeCommand>, ApplySterilisationOutcomeCommandHandler>();
        services.AddScoped<IValidator<ApplySterilisationOutcomeCommand>, ApplySterilisationOutcomeCommandValidator>();

        services.AddScoped<IQueryHandlerAsync<GetSurgicalReportQuery, SurgicalReportViewModel?>, GetSurgicalReportQueryHandler>();

        // ADR-166: the neuter status is set by a CAT-context handler on the sterilisation event; the surgery never writes the cat.
        services.AddScoped<IEventHandlerAsync<SterilisationCompletedEvent>, CatEcommerce.Application.DomainEventHandlers.Cat.SetNeuterStatusOnSterilisationHandler>();
    }

    private static void RegisterRecoveryAndDischarge(IServiceCollection services)
    {
        services.AddScoped<ICommandHandlerAsync<SetRecoveryStatusCommand>, SetRecoveryStatusCommandHandler>();
        services.AddScoped<IValidator<SetRecoveryStatusCommand>, SetRecoveryStatusCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<DischargeSurgeryCommand>, DischargeSurgeryCommandHandler>();
        services.AddScoped<IValidator<DischargeSurgeryCommand>, DischargeSurgeryCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CompleteSurgeryFollowUpCommand>, CompleteSurgeryFollowUpCommandHandler>();
        services.AddScoped<IValidator<CompleteSurgeryFollowUpCommand>, CompleteSurgeryFollowUpCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CancelSurgeryFollowUpCommand>, CancelSurgeryFollowUpCommandHandler>();
        services.AddScoped<IValidator<CancelSurgeryFollowUpCommand>, CancelSurgeryFollowUpCommandValidator>();

        services.AddScoped<IQueryHandlerAsync<GetRecoveryBoardQuery, RecoveryBoardViewModel>, GetRecoveryBoardQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDischargeDraftQuery, DischargeDraftViewModel?>, GetDischargeDraftQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerSurgeriesQuery, PagedResult<CustomerSurgerySummaryViewModel>>, GetCustomerSurgeriesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerSurgeryDetailQuery, CustomerSurgeryDetailViewModel?>, GetCustomerSurgeryDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCatSurgeriesQuery, PagedResult<CatSurgeryHistoryViewModel>>, GetCatSurgeriesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetSurgeryChargeableItemsQuery, SurgeryChargeableItemsViewModel?>, GetSurgeryChargeableItemsQueryHandler>();
    }

    private static void RegisterCampaigns(IServiceCollection services)
    {
        services.AddScoped<ICommandHandlerAsync<CreateSurgeryCampaignCommand>, CreateSurgeryCampaignCommandHandler>();
        services.AddScoped<IValidator<CreateSurgeryCampaignCommand>, CreateSurgeryCampaignCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<UpdateSurgeryCampaignCommand>, UpdateSurgeryCampaignCommandHandler>();
        services.AddScoped<IValidator<UpdateSurgeryCampaignCommand>, UpdateSurgeryCampaignCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SetSurgeryCampaignActiveCommand>, SetSurgeryCampaignActiveCommandHandler>();
        services.AddScoped<IValidator<SetSurgeryCampaignActiveCommand>, SetSurgeryCampaignActiveCommandValidator>();

        services.AddScoped<IQueryHandlerAsync<GetSurgeryCampaignsQuery, PagedResult<SurgeryCampaignViewModel>>, GetSurgeryCampaignsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetApplicableCampaignsQuery, IReadOnlyList<ApplicableCampaignViewModel>>, GetApplicableCampaignsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCampaignCasesQuery, CampaignCasesViewModel?>, GetCampaignCasesQueryHandler>();
    }

    private static void RegisterAnaesthesiaLog(IServiceCollection services)
    {
        services.AddScoped<ICommandHandlerAsync<LogAnaesthesiaDrugCommand>, LogAnaesthesiaDrugCommandHandler>();
        services.AddScoped<IValidator<LogAnaesthesiaDrugCommand>, LogAnaesthesiaDrugCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<LogAnaesthesiaVitalsCommand>, LogAnaesthesiaVitalsCommandHandler>();
        services.AddScoped<IValidator<LogAnaesthesiaVitalsCommand>, LogAnaesthesiaVitalsCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<LogAnaesthesiaMilestoneCommand>, LogAnaesthesiaMilestoneCommandHandler>();
        services.AddScoped<IValidator<LogAnaesthesiaMilestoneCommand>, LogAnaesthesiaMilestoneCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CorrectAnaesthesiaEntryCommand>, CorrectAnaesthesiaEntryCommandHandler>();
        services.AddScoped<IValidator<CorrectAnaesthesiaEntryCommand>, CorrectAnaesthesiaEntryCommandValidator>();

        services.AddScoped<IQueryHandlerAsync<GetAnaesthesiaLogQuery, AnaesthesiaLogViewModel?>, GetAnaesthesiaLogQueryHandler>();

        services.AddScoped<IEventHandlerAsync<AnaesthesiaEntryLoggedEvent>, AuditControlledDrugEntryHandler>();
    }

    private static void RegisterChecklistAndStart(IServiceCollection services)
    {
        services.AddScoped<ICommandHandlerAsync<CompleteChecklistItemCommand>, CompleteChecklistItemCommandHandler>();
        services.AddScoped<IValidator<CompleteChecklistItemCommand>, CompleteChecklistItemCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<WaiveChecklistItemCommand>, WaiveChecklistItemCommandHandler>();
        services.AddScoped<IValidator<WaiveChecklistItemCommand>, WaiveChecklistItemCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordAsaGradeCommand>, RecordAsaGradeCommandHandler>();
        services.AddScoped<IValidator<RecordAsaGradeCommand>, RecordAsaGradeCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<AdmitSurgeryCommand>, AdmitSurgeryCommandHandler>();
        services.AddScoped<IValidator<AdmitSurgeryCommand>, AdmitSurgeryCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<StartSurgeryCommand>, StartSurgeryCommandHandler>();
        services.AddScoped<IValidator<StartSurgeryCommand>, StartSurgeryCommandValidator>();

        services.AddScoped<IEventHandlerAsync<CatWeightRecordedEvent>, CompleteWeightItemOnWeightRecordedHandler>();
        services.AddScoped<IEventHandlerAsync<ConsentRecordedEvent>, CompleteConsentItemOnConsentRecordedHandler>();
        services.AddScoped<IEventHandlerAsync<ConsentWithdrawnEvent>, RevertConsentItemOnConsentWithdrawnHandler>();
        services.AddScoped<IEventHandlerAsync<SurgeryConsentOverriddenEvent>, AuditConsentOverrideHandler>();
    }
}
