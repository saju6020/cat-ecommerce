// Administration module -- HO-ADM-001.
//
// Registers everything Administration and Configuration needs: command handlers + validators,
// query handlers, domain event handlers, the settings cache, the branch calendar and active-branch
// accessor. Moved out of Program.cs by ADR-341 so this area never needs to touch a file another
// area also edits.

using CatEcommerce.Api.Branches;
using CatEcommerce.Application.CommandHandlers.Administration;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Application.DomainEventHandlers.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Retention.Erasers;
using CatEcommerce.Infrastructure.Scheduling;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Read.QueryHandlers.Administration;
using CatEcommerce.Read.QueryHandlers.Cat;
using CatEcommerce.Read.ViewModels;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Events;
using Platform.Infrastructure.Core.Queries;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Api.Modules;

public static class AdministrationModule
{
    public static IServiceCollection AddAdministrationModule(this IServiceCollection services, IConfiguration configuration)
    {
        // ── Phase A: authorization -- GetCatOwnerQuery backs RequireActionFilter's customer
        // ownership scoping (§12.3); it is dispatched, never called on a controller. ──────────────
        services.AddScoped<IQueryHandlerAsync<GetCatOwnerQuery, Guid?>, GetCatOwnerQueryHandler>();

        // Gate fix 2026-09-26 (TEST gate I-E3): the audit-export row cap is bound from
        // configuration (default 50,000, AuditExportRules.MaxRows) instead of being a hard-coded
        // constant only -- a test factory can now override "AuditExport:MaxRows" the same way
        // AccountsApiFactory.WithTightRateLimits() overrides rate limits.
        // Gate fix 2026-09-26 (CODE_REVIEW seventh pass, MEDIUM): validated at startup, the same
        // ValidateDataAnnotations().ValidateOnStart() pattern AccountsModule already uses for
        // AccountOptions -- a misconfigured MaxRows (zero, negative, or above the product's own
        // cap) fails the host at boot instead of silently disabling the cap or overflowing it.
        services.AddOptions<AuditExportOptions>()
            .Bind(configuration.GetSection(AuditExportOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // ── §8.9 ports: real implementations replace the FAKE registrations HO-ADM-001's
        // consumers used until this module landed (docs/handover/CONTRACTS.md #18-#20). ──────────
        services.AddMemoryCache();
        services.AddScoped<IBusinessSettings, CachedBusinessSettings>();
        services.AddScoped<IBranchCalendar, BranchCalendarService>();
        services.AddScoped<IActiveBranchAccessor, HttpContextActiveBranchAccessor>();
        services.AddScoped<ISettingsCacheInvalidator, MemoryCacheSettingsCacheInvalidator>();
        services.AddHttpContextAccessor();

        // HO-ACC-001's §12.2 rewire: security.staffIdleTimeoutMinutes now comes from settings,
        // with AccountOptions.StaffIdleTimeoutMinutes surviving only as the registry's seed
        // default. Registered AFTER AccountsModule (Program.cs's module order), so this wins the
        // direct (non-IEnumerable) ISessionPolicy resolution without editing AccountsModule.cs.
        services.AddScoped<ISessionPolicy, BusinessSettingsSessionPolicy>();

        // ── HO-XCT-001 C-ALL-2: Administration stores a customer's personal data beyond the id
        // (PolicyAcceptanceAggregate.IpAddress) -- register the eraser now (§5.2, CONTRACTS.md
        // §"Cross-cutting duties"), ahead of HO-XCT-001 itself landing. ─────────────────────────
        services.AddScoped<IPersonalDataEraser, AdministrationPersonalDataEraser>();

        // §12.5/§12.7: a global action filter that no-ops on every action except one carrying
        // [StorefrontEndpoint] -- none does yet in this slice (ADR-053).
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(o => o.Filters.Add<CatEcommerce.Api.Filters.MaintenanceModeFilter>());

        // Gate fix 2026-09-26 (SEC LOW, hardening round): NulByteRejectionFilter -- one API-wide
        // guard against a NUL byte anywhere in a bound request -- was registered here as a global
        // MvcOptions filter. Integrator move, 2026-09-26 (HO-ADM-001-ACCEPTANCE integrator note,
        // HO-ADM-001-ARCHITECTURE MEDIUM 1): it is now registered from Program.cs's own
        // AddControllers(...) call, alongside ConcurrencyExceptionFilter and
        // UniqueConstraintExceptionFilter, since it is host-wide and not Administration-specific --
        // see Program.cs.

        RegisterCommands(services);
        RegisterQueries(services);
        RegisterDomainEventHandlers(services);

        return services;
    }

    private static void RegisterCommands(IServiceCollection services)
    {
        services.AddScoped<ICommandHandlerAsync<CreateBranchCommand>, CreateBranchCommandHandler>();
        services.AddScoped<IValidator<CreateBranchCommand>, CreateBranchCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateBranchDetailsCommand>, UpdateBranchDetailsCommandHandler>();
        services.AddScoped<IValidator<UpdateBranchDetailsCommand>, UpdateBranchDetailsCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetBranchWeeklyHoursCommand>, SetBranchWeeklyHoursCommandHandler>();
        services.AddScoped<IValidator<SetBranchWeeklyHoursCommand>, SetBranchWeeklyHoursCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetBranchActivationCommand>, SetBranchActivationCommandHandler>();
        services.AddScoped<IValidator<SetBranchActivationCommand>, SetBranchActivationCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CreateBranchClosureCommand>, CreateBranchClosureCommandHandler>();
        services.AddScoped<IValidator<CreateBranchClosureCommand>, CreateBranchClosureCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CancelBranchClosureCommand>, CancelBranchClosureCommandHandler>();
        services.AddScoped<IValidator<CancelBranchClosureCommand>, CancelBranchClosureCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CreateRoomCommand>, CreateRoomCommandHandler>();
        services.AddScoped<IValidator<CreateRoomCommand>, CreateRoomCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateRoomCommand>, UpdateRoomCommandHandler>();
        services.AddScoped<IValidator<UpdateRoomCommand>, UpdateRoomCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<AssignStaffBranchesCommand>, AssignStaffBranchesCommandHandler>();
        services.AddScoped<IValidator<AssignStaffBranchesCommand>, AssignStaffBranchesCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetRosterPatternCommand>, SetRosterPatternCommandHandler>();
        services.AddScoped<IValidator<SetRosterPatternCommand>, SetRosterPatternCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CancelScheduledRosterPatternCommand>, CancelScheduledRosterPatternCommandHandler>();
        services.AddScoped<IValidator<CancelScheduledRosterPatternCommand>, CancelScheduledRosterPatternCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CreateRosterExceptionCommand>, CreateRosterExceptionCommandHandler>();
        services.AddScoped<IValidator<CreateRosterExceptionCommand>, CreateRosterExceptionCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CancelRosterExceptionCommand>, CancelRosterExceptionCommandHandler>();
        services.AddScoped<IValidator<CancelRosterExceptionCommand>, CancelRosterExceptionCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RequestLeaveCommand>, RequestLeaveCommandHandler>();
        services.AddScoped<IValidator<RequestLeaveCommand>, RequestLeaveCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ApproveLeaveCommand>, ApproveLeaveCommandHandler>();
        services.AddScoped<IValidator<ApproveLeaveCommand>, ApproveLeaveCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RejectLeaveCommand>, RejectLeaveCommandHandler>();
        services.AddScoped<IValidator<RejectLeaveCommand>, RejectLeaveCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CancelLeaveCommand>, CancelLeaveCommandHandler>();
        services.AddScoped<IValidator<CancelLeaveCommand>, CancelLeaveCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CreateClinicServiceCommand>, CreateClinicServiceCommandHandler>();
        services.AddScoped<IValidator<CreateClinicServiceCommand>, CreateClinicServiceCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateClinicServiceCommand>, UpdateClinicServiceCommandHandler>();
        services.AddScoped<IValidator<UpdateClinicServiceCommand>, UpdateClinicServiceCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetClinicServiceActivationCommand>, SetClinicServiceActivationCommandHandler>();
        services.AddScoped<IValidator<SetClinicServiceActivationCommand>, SetClinicServiceActivationCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetServicePriceCommand>, SetServicePriceCommandHandler>();
        services.AddScoped<IValidator<SetServicePriceCommand>, SetServicePriceCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CreateTaxClassCommand>, CreateTaxClassCommandHandler>();
        services.AddScoped<IValidator<CreateTaxClassCommand>, CreateTaxClassCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateTaxClassCommand>, UpdateTaxClassCommandHandler>();
        services.AddScoped<IValidator<UpdateTaxClassCommand>, UpdateTaxClassCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetTaxRateCommand>, SetTaxRateCommandHandler>();
        services.AddScoped<IValidator<SetTaxRateCommand>, SetTaxRateCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ChangeBusinessSettingsCommand>, ChangeBusinessSettingsCommandHandler>();
        services.AddScoped<IValidator<ChangeBusinessSettingsCommand>, ChangeBusinessSettingsCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ClearBranchSettingOverrideCommand>, ClearBranchSettingOverrideCommandHandler>();
        services.AddScoped<IValidator<ClearBranchSettingOverrideCommand>, ClearBranchSettingOverrideCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordAuditLogExportCommand>, RecordAuditLogExportCommandHandler>();
        services.AddScoped<IValidator<RecordAuditLogExportCommand>, RecordAuditLogExportCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CreateContentItemCommand>, CreateContentItemCommandHandler>();
        services.AddScoped<IValidator<CreateContentItemCommand>, CreateContentItemCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<EditContentDraftCommand>, EditContentDraftCommandHandler>();
        services.AddScoped<IValidator<EditContentDraftCommand>, EditContentDraftCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<PublishContentItemCommand>, PublishContentItemCommandHandler>();
        services.AddScoped<IValidator<PublishContentItemCommand>, PublishContentItemCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UnpublishContentItemCommand>, UnpublishContentItemCommandHandler>();
        services.AddScoped<IValidator<UnpublishContentItemCommand>, UnpublishContentItemCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CreatePolicyDocumentCommand>, CreatePolicyDocumentCommandHandler>();
        services.AddScoped<IValidator<CreatePolicyDocumentCommand>, CreatePolicyDocumentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<PublishPolicyVersionCommand>, PublishPolicyVersionCommandHandler>();
        services.AddScoped<IValidator<PublishPolicyVersionCommand>, PublishPolicyVersionCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<AcceptPolicyVersionCommand>, AcceptPolicyVersionCommandHandler>();
        services.AddScoped<IValidator<AcceptPolicyVersionCommand>, AcceptPolicyVersionCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetMaintenanceModeCommand>, SetMaintenanceModeCommandHandler>();
        services.AddScoped<IValidator<SetMaintenanceModeCommand>, SetMaintenanceModeCommandValidator>();
    }

    private static void RegisterQueries(IServiceCollection services)
    {
        services.AddScoped<IQueryHandlerAsync<GetBranchesQuery, IReadOnlyList<BranchSummaryViewModel>>, GetBranchesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetAdminBranchesQuery, IReadOnlyList<AdminBranchViewModel>>, GetAdminBranchesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetAdminBranchQuery, AdminBranchViewModel?>, GetAdminBranchQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBranchClosuresQuery, IReadOnlyList<BranchClosureViewModel>>, GetBranchClosuresQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRoomsQuery, IReadOnlyList<RoomViewModel>>, GetRoomsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBranchCalendarQuery, IReadOnlyList<TimeIntervalViewModel>>, GetBranchCalendarQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaffBranchAssignmentQuery, StaffBranchAssignmentViewModel?>, GetStaffBranchAssignmentQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetMyBranchesQuery, StaffBranchAssignmentViewModel?>, GetMyBranchesQueryHandler>();

        services.AddScoped<IQueryHandlerAsync<GetBranchRostersQuery, IReadOnlyList<StaffRosterViewModel>>, GetBranchRostersQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRosterExceptionsQuery, IReadOnlyList<RosterExceptionViewModel>>, GetRosterExceptionsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaffAvailabilityQuery, IReadOnlyList<StaffAvailableIntervalViewModel>>, GetStaffAvailabilityQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetMyLeaveRequestsQuery, IReadOnlyList<LeaveRequestViewModel>>, GetMyLeaveRequestsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetLeaveRequestsQuery, PagedResult<LeaveRequestViewModel>>, GetLeaveRequestsQueryHandler>();

        services.AddScoped<IQueryHandlerAsync<GetPublicServicesQuery, IReadOnlyList<PublicServiceViewModel>>, GetPublicServicesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetAdminServicesQuery, IReadOnlyList<AdminServiceViewModel>>, GetAdminServicesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetAdminServiceQuery, AdminServiceViewModel?>, GetAdminServiceQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetServicePricesQuery, IReadOnlyList<ServicePriceViewModel>>, GetServicePricesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetTaxClassesQuery, IReadOnlyList<TaxClassViewModel>>, GetTaxClassesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetTaxRatesQuery, IReadOnlyList<TaxRateViewModel>>, GetTaxRatesQueryHandler>();

        services.AddScoped<IQueryHandlerAsync<GetSettingsQuery, IReadOnlyList<SettingViewModel>>, GetSettingsQueryHandler>();

        services.AddScoped<IQueryHandlerAsync<GetAuditEntriesQuery, PagedResult<AuditEntryViewModel>>, GetAuditEntriesQueryHandler>();

        services.AddScoped<IQueryHandlerAsync<GetAdminContentQuery, IReadOnlyList<ContentItemViewModel>>, GetAdminContentQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetAdminContentItemQuery, ContentItemViewModel?>, GetAdminContentItemQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPublishedContentQuery, PublishedContentViewModel?>, GetPublishedContentQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPublishedBannersQuery, IReadOnlyList<PublishedContentViewModel>>, GetPublishedBannersQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPublishedFaqsQuery, IReadOnlyList<PublishedContentViewModel>>, GetPublishedFaqsQueryHandler>();

        services.AddScoped<IQueryHandlerAsync<GetAdminPoliciesQuery, IReadOnlyList<PolicyDocumentViewModel>>, GetAdminPoliciesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPublicPolicyQuery, PolicyVersionViewModel?>, GetPublicPolicyQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPendingPolicyAcceptancesQuery, IReadOnlyList<PolicyVersionViewModel>>, GetPendingPolicyAcceptancesQueryHandler>();

        services.AddScoped<IQueryHandlerAsync<GetSystemStatusQuery, SystemStatusViewModel>, GetSystemStatusQueryHandler>();
    }

    private static void RegisterDomainEventHandlers(IServiceCollection services)
    {
        services.AddScoped<IEventHandlerAsync<BusinessSettingsChangedEvent>, InvalidateSettingsCacheHandler>();
        services.AddScoped<IEventHandlerAsync<StaffLeaveApprovedEvent>, AuditLeaveDecisionHandler>();
        services.AddScoped<IEventHandlerAsync<LeaveRejectedEvent>, AuditLeaveDecisionHandler>();
        services.AddScoped<IEventHandlerAsync<LeaveCancelledEvent>, AuditLeaveDecisionHandler>();
        services.AddScoped<IEventHandlerAsync<MaintenanceModeChangedEvent>, AuditMaintenanceModeHandler>();
    }
}
