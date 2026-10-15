// Pharmacy module -- HO-PHM-001 (Pharmacy and Prescriptions).
//
// Registered once from Program.cs (ADR-341); this file is the only place the area's handlers, validators, query handlers, domain event handlers,
// ports and worker are wired, so Program.cs never needs another edit and no other area's module file is touched. There is no assembly scanning
// (ADR-018): every handler and validator is listed here, validators as IValidator<T> -- registered as the AbstractValidator base class a validator
// silently never runs. Built phase by phase; each phase appends its own section.

using CatEcommerce.Api.Background;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.CommandHandlers.Prescription;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Prescription;
using CatEcommerce.Application.DomainEventHandlers.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Events.Prescription;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Infrastructure.Notifications.Guards;
using CatEcommerce.Infrastructure.Pharmacy;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Read.QueryHandlers.Prescription;
using CatEcommerce.Read.ViewModels;
using FluentValidation;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Events;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Api.Modules;

public static class PharmacyModule
{
    public static IServiceCollection AddPharmacyModule(this IServiceCollection services, IConfiguration configuration)
    {
        AddPorts(services);
        AddFormulary(services);
        AddPrescribing(services);
        AddControlledDrugRegister(services);
        AddDispensing(services);
        AddRepeatsAndPortal(services);
        AddDietAuthorisations(services);
        AddWorkerAndLimits(services);

        return services;
    }

    private static void AddPorts(IServiceCollection services)
    {
        // HO-PHM-001 §8.12 -- Pharmacy's read adapters over the catalogue (HO-SHP-001), the branch calendar (HO-ADM-001) and the formulary itself.
        services.AddScoped<ICatalogueReader, CatalogueReader>();
        services.AddScoped<ISkuPriceReader, SkuPriceReader>();
        services.AddScoped<IPharmacyClock, PharmacyClock>();
        services.AddScoped<IFormularyReader, FormularyReader>();

        // Phase C -- the safety input (one loader for the check query, issue and renew) and the prescription number sequence.
        services.AddScoped<ISafetyInputLoader, SafetyInputLoader>();
        services.AddScoped<IPrescriptionNumberAllocator, PrescriptionNumberAllocator>();
        services.AddScoped<PrescribingPorts>();
    }

    private static void AddFormulary(IServiceCollection services)
    {
        // Phase B -- C1-C4 (US-PHM-04 AC1)
        services.AddScoped<ICommandHandlerAsync<CreateFormularyMedicineCommand>, CreateFormularyMedicineCommandHandler>();
        services.AddScoped<IValidator<CreateFormularyMedicineCommand>, CreateFormularyMedicineCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<UpdateFormularyMedicineCommand>, UpdateFormularyMedicineCommandHandler>();
        services.AddScoped<IValidator<UpdateFormularyMedicineCommand>, UpdateFormularyMedicineCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SetFormularyMedicineControlledCommand>, SetFormularyMedicineControlledCommandHandler>();
        services.AddScoped<IValidator<SetFormularyMedicineControlledCommand>, SetFormularyMedicineControlledCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SetFormularyMedicineActiveCommand>, SetFormularyMedicineActiveCommandHandler>();
        services.AddScoped<IValidator<SetFormularyMedicineActiveCommand>, SetFormularyMedicineActiveCommandValidator>();

        // §11.6 -- BusinessEvent audit rows. They never enqueue and never mutate a clinical aggregate.
        services.AddScoped<IEventHandlerAsync<FormularyMedicineControlledStatusChangedEvent>, AuditFormularyControlledStatusHandler>();

        services.AddScoped<IQueryHandlerAsync<GetFormularyMedicinesQuery, PagedResult<FormularyMedicineSummaryViewModel>>, GetFormularyMedicinesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetFormularyMedicineQuery, FormularyMedicineDetailViewModel?>, GetFormularyMedicineQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<SearchFormularySkusQuery, IReadOnlyList<FormularySkuSearchResultViewModel>>, SearchFormularySkusQueryHandler>();
    }

    private static void AddPrescribing(IServiceCollection services)
    {
        // Phase C -- C5 issue, C6 cancel (US-PHM-01, 02, 03)
        services.AddScoped<ICommandHandlerAsync<IssuePrescriptionCommand>, IssuePrescriptionCommandHandler>();
        services.AddScoped<IValidator<IssuePrescriptionCommand>, IssuePrescriptionCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CancelPrescriptionCommand>, CancelPrescriptionCommandHandler>();
        services.AddScoped<IValidator<CancelPrescriptionCommand>, CancelPrescriptionCommandValidator>();

        // §11.6 -- BusinessEvent audit rows. They never enqueue and never mutate a clinical aggregate.
        services.AddScoped<IEventHandlerAsync<PrescriptionIssuedEvent>, AuditPrescriptionAcknowledgementHandler>();

        services.AddScoped<IQueryHandlerAsync<CheckPrescriptionSafetyQuery, SafetyAssessmentViewModel?>, CheckPrescriptionSafetyQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCatPrescriptionsQuery, PagedResult<PrescriptionSummaryViewModel>>, GetCatPrescriptionsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPrescriptionDetailQuery, PrescriptionDetailViewModel?>, GetPrescriptionDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetVisitPrescriptionsQuery, IReadOnlyList<PrescriptionSummaryViewModel>>, GetVisitPrescriptionsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCatPrescriptionHistoryQuery, IReadOnlyList<PrescriptionSummaryViewModel>>, GetCatPrescriptionHistoryQueryHandler>();
    }

    private static void AddDispensing(IServiceCollection services)
    {
        // Phase E -- C9 dispense, C10 label print, C11 reverse (US-PHM-05, US-PHM-04 AC2)
        services.AddScoped<ICommandHandlerAsync<DispensePrescriptionCommand>, DispensePrescriptionCommandHandler>();
        services.AddScoped<IValidator<DispensePrescriptionCommand>, DispensePrescriptionCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordLabelPrintCommand>, RecordLabelPrintCommandHandler>();
        services.AddScoped<IValidator<RecordLabelPrintCommand>, RecordLabelPrintCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ReverseDispensingCommand>, ReverseDispensingCommandHandler>();
        services.AddScoped<IValidator<ReverseDispensingCommand>, ReverseDispensingCommandValidator>();

        // §11.6 -- a reprint is counted and audited.
        services.AddScoped<IEventHandlerAsync<LabelPrintedEvent>, AuditLabelPrintedHandler>();

        services.AddScoped<IQueryHandlerAsync<GetPharmacyQueueQuery, PharmacyQueueViewModel>, GetPharmacyQueueQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<LookupPrescriptionByNumberQuery, PrescriptionLookupViewModel?>, LookupPrescriptionByNumberQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDispensePlanQuery, DispensePlanViewModel?>, GetDispensePlanQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDispensingQuery, DispensingViewModel?>, GetDispensingQueryHandler>();
    }

    private static void AddControlledDrugRegister(IServiceCollection services)
    {
        // Phase D -- the register's contract (HO-SRG-001, HO-EMR-001 and HO-IMP-001 write through it) and the witness verifier (ADR-146).
        services.AddScoped<IControlledDrugRegister, EfControlledDrugRegister>();
        services.AddScoped<IControlledDrugDirectory, ControlledDrugDirectory>();
        services.AddScoped<IStaffWitnessVerifier, StaffWitnessVerifier>();
        services.AddScoped<IRegisterChain, EfRegisterChain>();

        services.AddScoped<ICommandHandlerAsync<RecordControlledDrugCorrectionCommand>, RecordControlledDrugCorrectionCommandHandler>();
        services.AddScoped<IValidator<RecordControlledDrugCorrectionCommand>, RecordControlledDrugCorrectionCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SealControlledDrugEntriesCommand>, SealControlledDrugEntriesCommandHandler>();
        services.AddScoped<IValidator<SealControlledDrugEntriesCommand>, SealControlledDrugEntriesCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordControlledDrugRegisterExportCommand>, RecordControlledDrugRegisterExportCommandHandler>();
        services.AddScoped<IValidator<RecordControlledDrugRegisterExportCommand>, RecordControlledDrugRegisterExportCommandValidator>();

        services.AddScoped<IQueryHandlerAsync<GetControlledDrugRegisterQuery, PagedResult<RegisterEntryViewModel>>, GetControlledDrugRegisterQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetControlledDrugEntryQuery, RegisterEntryDetailViewModel?>, GetControlledDrugEntryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetControlledDrugBalancesQuery, IReadOnlyList<RegisterBalanceViewModel>>, GetControlledDrugBalancesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<VerifyControlledDrugRegisterQuery, RegisterVerificationViewModel>, VerifyControlledDrugRegisterQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<ExportControlledDrugRegisterQuery, IReadOnlyList<RegisterExportRow>>, ExportControlledDrugRegisterQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBranchesWithUnsealedRegisterEntriesQuery, IReadOnlyList<Guid>>, GetBranchesWithUnsealedRegisterEntriesQueryHandler>();
    }

    private static void AddRepeatsAndPortal(IServiceCollection services)
    {
        // Phase F -- C12-C16 repeats and renewal (US-PHM-06, US-PHM-07)
        services.AddScoped<ICommandHandlerAsync<RequestRepeatCommand>, RequestRepeatCommandHandler>();
        services.AddScoped<IValidator<RequestRepeatCommand>, RequestRepeatCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<WithdrawRepeatRequestCommand>, WithdrawRepeatRequestCommandHandler>();
        services.AddScoped<IValidator<WithdrawRepeatRequestCommand>, WithdrawRepeatRequestCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<DeclineRepeatRequestCommand>, DeclineRepeatRequestCommandHandler>();
        services.AddScoped<IValidator<DeclineRepeatRequestCommand>, DeclineRepeatRequestCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RenewPrescriptionCommand>, RenewPrescriptionCommandHandler>();
        services.AddScoped<IValidator<RenewPrescriptionCommand>, RenewPrescriptionCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ExpirePrescriptionCommand>, ExpirePrescriptionCommandHandler>();
        services.AddScoped<IValidator<ExpirePrescriptionCommand>, ExpirePrescriptionCommandValidator>();

        services.AddScoped<IQueryHandlerAsync<GetCustomerPrescriptionsQuery, IReadOnlyList<CustomerPrescriptionViewModel>>, GetCustomerPrescriptionsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerPrescriptionDetailQuery, CustomerPrescriptionDetailViewModel?>, GetCustomerPrescriptionDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetExpirablePrescriptionsQuery, IReadOnlyList<Guid>>, GetExpirablePrescriptionsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDueRefillRemindersQuery, IReadOnlyList<DueRefillReminderViewModel>>, GetDueRefillRemindersQueryHandler>();

        // US-NTF-03 AC1 -- a refill dispensed (or a prescription ended) before the reminder is sent cancels it. Resolved by HO-NTF-001's SubjectGuardResolver.
        services.AddScoped<INotificationSubjectGuard, PrescriptionRefillDueGuard>();
    }

    private static void AddDietAuthorisations(IServiceCollection services)
    {
        // Phase G -- C17-C19 (US-PHM-08, ADR-149)
        services.AddScoped<ICommandHandlerAsync<AuthoriseDietCommand>, AuthoriseDietCommandHandler>();
        services.AddScoped<IValidator<AuthoriseDietCommand>, AuthoriseDietCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ExtendDietAuthorisationCommand>, ExtendDietAuthorisationCommandHandler>();
        services.AddScoped<IValidator<ExtendDietAuthorisationCommand>, ExtendDietAuthorisationCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RevokeDietAuthorisationCommand>, RevokeDietAuthorisationCommandHandler>();
        services.AddScoped<IValidator<RevokeDietAuthorisationCommand>, RevokeDietAuthorisationCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ExpireDietAuthorisationCommand>, ExpireDietAuthorisationCommandHandler>();
        services.AddScoped<IValidator<ExpireDietAuthorisationCommand>, ExpireDietAuthorisationCommandValidator>();

        services.AddScoped<IQueryHandlerAsync<GetCatDietAuthorisationsQuery, IReadOnlyList<DietAuthorisationViewModel>>, GetCatDietAuthorisationsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetVisitDietAuthorisationsQuery, IReadOnlyList<DietAuthorisationViewModel>>, GetVisitDietAuthorisationsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<SearchDietProductsQuery, IReadOnlyList<DietProductViewModel>>, SearchDietProductsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerDietAuthorisationsQuery, IReadOnlyList<CustomerDietAuthorisationViewModel>>, GetCustomerDietAuthorisationsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetExpirableDietAuthorisationsQuery, IReadOnlyList<Guid>>, GetExpirableDietAuthorisationsQueryHandler>();

        // The real gate (replaces the fail-closed DenyAllDietAuthorisationGate fake; HO-ORD-001 and HO-SHP-001 consume it).
        services.AddScoped<IDietAuthorisationGate, DietAuthorisationGate>();

        // ADR-152 -- HO-ACC-001's CustomerMergedEvent moves prescriptions, dispensings and diet authorisations (never register entries).
        services.AddScoped<IPharmacyRecordFinder, EfPharmacyRecordFinder>();

        // C-ALL-3 -- clinical records past retention are flagged for review (never deleted).
        services.AddScoped<IRetentionReviewSource, PharmacyRetentionReviewSource>();
        services.AddScoped<IEventHandlerAsync<CustomerMergedEvent>, ReassignPharmacyRecordsOnCustomerMergedHandler>();
    }

    private static void AddWorkerAndLimits(IServiceCollection services)
    {
        // The pharmacy worker (§12.2): options bound from "Pharmacy", one scoped work cycle, one hosted service.
        services.AddOptions<PharmacyOptions>()
            .BindConfiguration(PharmacyOptions.SectionName)
            .Validate(o => o.Worker.IntervalSeconds >= 1, "Pharmacy:Worker:IntervalSeconds must be at least 1.")
            .Validate(o => o.Worker.RefillSweepIntervalMinutes >= 1, "Pharmacy:Worker:RefillSweepIntervalMinutes must be at least 1.")
            .ValidateOnStart();
        services.AddSingleton<PharmacyWorkerState>();
        services.AddScoped<PharmacyWorkCycle>();
        services.AddHostedService<PharmacyWorker>();

        // §12.7 -- additive AddRateLimiter call, per ADR-341. The witness limit is per staff member (a guess at a colleague's password at the counter is throttled before the
        // witness account's own lockout), the repeat-request limit per customer; falling back to the IP only when no session is resolved.
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PharmacyRateLimitPolicies.Witness, context => PartitionedLimiter(context, ResolvedCallerKind.Staff, o => o.Witness));
            options.AddPolicy(PharmacyRateLimitPolicies.RepeatRequest, context => PartitionedLimiter(context, ResolvedCallerKind.Customer, o => o.RepeatRequest));
        });
    }

    private static System.Threading.RateLimiting.RateLimitPartition<string> PartitionedLimiter(
        Microsoft.AspNetCore.Http.HttpContext context, ResolvedCallerKind kind, Func<PharmacyOptions, RateLimitPolicyOptions> select)
    {
        var limits = select(context.RequestServices.GetRequiredService<IOptions<PharmacyOptions>>().Value);
        var caller = context.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller;
        var partitionKey = caller is not null && caller.Kind == kind
            ? $"{kind}:{caller.AccountId}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            { PermitLimit = limits.PermitLimit, Window = TimeSpan.FromMinutes(limits.WindowMinutes), QueueLimit = 0 });
    }
}
