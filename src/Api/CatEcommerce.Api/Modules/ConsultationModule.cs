// Consultation module -- HO-EMR-001 (Consultation and Medical Records).
//
// Registered once from Program.cs (ADR-341); this file is the only place the area's handlers,
// validators, query handlers, domain event handlers and filters are wired, so Program.cs never needs
// another edit and no other area's module file is touched. There is no assembly scanning (ADR-018):
// every handler and validator is listed here, validators as IValidator<T> -- registered as the
// AbstractValidator base class a validator silently never runs.
//
// Phase F (treatments and stock, US-CLN-15) is registered in AddTreatments below. It consumes Inventory's IStockLedger,
// HO-PHM-001's ISkuPriceReader and IControlledDrugDirectory and Billing's IBillingPricingInputs, all registered by their own
// modules; nothing of theirs is registered here.

using CatEcommerce.Api.Filters;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.CommandHandlers.Visit;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Application.DomainEventHandlers.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Infrastructure.Consultation;
using CatEcommerce.Infrastructure.Documents;
using CatEcommerce.Infrastructure.Notifications.Guards;
using CatEcommerce.Infrastructure.Retention;
using CatEcommerce.Read.ClinicalHistory;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Read.QueryHandlers.Visit;
using CatEcommerce.Read.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Events;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Api.Modules;

public static class ConsultationModule
{
    public static IServiceCollection AddConsultationModule(this IServiceCollection services, IConfiguration configuration)
    {
        AddFilters(services);
        AddCatalogues(services);
        AddAccessLog(services);
        AddVisits(services);
        AddConsultationNotes(services);
        AddDiagnostics(services);
        AddTreatments(services);
        AddClinicalHistory(services);
        AddSummaryAndFollowUp(services);
        AddConsents(services);
        AddRetention(services);

        return services;
    }

    private static void AddFilters(IServiceCollection services)
    {
        // ADR-118 layer three: a CR001 from a clinical immutability trigger -> 409. Global, alongside the
        // concurrency filter registered by Program.cs; the filter itself takes a logger.
        services.Configure<MvcOptions>(options => options.Filters.Add<ClinicalRecordFrozenFilter>());
    }

    private static void AddCatalogues(IServiceCollection services)
    {
        // C1-C3 -- diagnosis codes, lab analytes, note templates.
        services.AddScoped<ICommandHandlerAsync<CreateDiagnosisCodeCommand>, CreateDiagnosisCodeCommandHandler>();
        services.AddScoped<IValidator<CreateDiagnosisCodeCommand>, CreateDiagnosisCodeCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<UpdateDiagnosisCodeCommand>, UpdateDiagnosisCodeCommandHandler>();
        services.AddScoped<IValidator<UpdateDiagnosisCodeCommand>, UpdateDiagnosisCodeCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SetDiagnosisCodeActiveCommand>, SetDiagnosisCodeActiveCommandHandler>();
        services.AddScoped<IValidator<SetDiagnosisCodeActiveCommand>, SetDiagnosisCodeActiveCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CreateLabAnalyteCommand>, CreateLabAnalyteCommandHandler>();
        services.AddScoped<IValidator<CreateLabAnalyteCommand>, CreateLabAnalyteCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<UpdateLabAnalyteCommand>, UpdateLabAnalyteCommandHandler>();
        services.AddScoped<IValidator<UpdateLabAnalyteCommand>, UpdateLabAnalyteCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SetLabAnalyteActiveCommand>, SetLabAnalyteActiveCommandHandler>();
        services.AddScoped<IValidator<SetLabAnalyteActiveCommand>, SetLabAnalyteActiveCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CreateNoteTemplateCommand>, CreateNoteTemplateCommandHandler>();
        services.AddScoped<IValidator<CreateNoteTemplateCommand>, CreateNoteTemplateCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<UpdateNoteTemplateCommand>, UpdateNoteTemplateCommandHandler>();
        services.AddScoped<IValidator<UpdateNoteTemplateCommand>, UpdateNoteTemplateCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SetNoteTemplateActiveCommand>, SetNoteTemplateActiveCommandHandler>();
        services.AddScoped<IValidator<SetNoteTemplateActiveCommand>, SetNoteTemplateActiveCommandValidator>();

        services.AddScoped<IQueryHandlerAsync<GetDiagnosisCodesQuery, PagedResult<DiagnosisCodeViewModel>>, GetDiagnosisCodesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDiagnosisCodeQuery, DiagnosisCodeViewModel?>, GetDiagnosisCodeQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetLabAnalytesQuery, PagedResult<LabAnalyteViewModel>>, GetLabAnalytesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetLabAnalyteQuery, LabAnalyteViewModel?>, GetLabAnalyteQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetNoteTemplatesQuery, PagedResult<NoteTemplateViewModel>>, GetNoteTemplatesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetNoteTemplateQuery, NoteTemplateViewModel?>, GetNoteTemplateQueryHandler>();
    }

    private static void AddVisits(IServiceCollection services)
    {
        // C5 open, C6 vitals (companion commit, ADR-121).
        services.AddScoped<ICommandHandlerAsync<OpenVisitCommand>, OpenVisitCommandHandler>();
        services.AddScoped<IValidator<OpenVisitCommand>, OpenVisitCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordVitalsCommand>, RecordVitalsCommandHandler>();
        services.AddScoped<IValidator<RecordVitalsCommand>, RecordVitalsCommandValidator>();

        services.AddScoped<IQueryHandlerAsync<GetVisitDetailQuery, VisitDetailViewModel?>, GetVisitDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetOpenVisitsQuery, IReadOnlyList<VisitListItemViewModel>>, GetOpenVisitsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetVisitByAppointmentQuery, VisitReferenceViewModel>, GetVisitByAppointmentQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetVitalSignRulesQuery, IReadOnlyList<VitalSignRuleViewModel>>, GetVitalSignRulesQueryHandler>();
    }

    private static void AddConsultationNotes(IServiceCollection services)
    {
        // C7 draft, C8 note, C9 finalise (Vet only, ADR-119), C10 addendum.
        services.AddScoped<ICommandHandlerAsync<SaveConsultationDraftCommand>, SaveConsultationDraftCommandHandler>();
        services.AddScoped<IValidator<SaveConsultationDraftCommand>, SaveConsultationDraftCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SaveConsultationNoteCommand>, SaveConsultationNoteCommandHandler>();
        services.AddScoped<IValidator<SaveConsultationNoteCommand>, SaveConsultationNoteCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<FinaliseVisitCommand>, FinaliseVisitCommandHandler>();
        services.AddScoped<IValidator<FinaliseVisitCommand>, FinaliseVisitCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<AddVisitAddendumCommand>, AddVisitAddendumCommandHandler>();
        services.AddScoped<IValidator<AddVisitAddendumCommand>, AddVisitAddendumCommandValidator>();

        // §11.6 -- BusinessEvent audit rows. They never enqueue and never mutate a clinical aggregate.
        services.AddScoped<IEventHandlerAsync<VisitFinalisedEvent>, AuditVisitFinalisedHandler>();
        services.AddScoped<IEventHandlerAsync<VisitAddendumAddedEvent>, AuditVisitAddendumHandler>();
    }

    private static void AddDiagnostics(IServiceCollection services)
    {
        // C12-C14 diagnostics and C15 photos. Files themselves are registered by HO-FIL-001's upload pipeline
        // (FileStorageModule); nothing here uploads, stores or serves a file.
        services.AddScoped<ICommandHandlerAsync<OrderDiagnosticCommand>, OrderDiagnosticCommandHandler>();
        services.AddScoped<IValidator<OrderDiagnosticCommand>, OrderDiagnosticCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordDiagnosticResultsCommand>, RecordDiagnosticResultsCommandHandler>();
        services.AddScoped<IValidator<RecordDiagnosticResultsCommand>, RecordDiagnosticResultsCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<AttachDiagnosticFileCommand>, AttachDiagnosticFileCommandHandler>();
        services.AddScoped<IValidator<AttachDiagnosticFileCommand>, AttachDiagnosticFileCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CancelDiagnosticOrderCommand>, CancelDiagnosticOrderCommandHandler>();
        services.AddScoped<IValidator<CancelDiagnosticOrderCommand>, CancelDiagnosticOrderCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<AttachClinicalPhotoCommand>, AttachClinicalPhotoCommandHandler>();
        services.AddScoped<IValidator<AttachClinicalPhotoCommand>, AttachClinicalPhotoCommandValidator>();

        services.AddScoped<IQueryHandlerAsync<GetDiagnosticOrderQuery, DiagnosticOrderViewModel?>, GetDiagnosticOrderQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPendingDiagnosticsQuery, IReadOnlyList<DiagnosticOrderListItemViewModel>>, GetPendingDiagnosticsQueryHandler>();
    }

    private static void AddTreatments(IServiceCollection services)
    {
        // C16-C17 treatments and stock (US-CLN-15, ADR-126, ADR-127): the stock moves through IStockLedger in the handler's own commit (F23).
        services.AddScoped<ICommandHandlerAsync<RecordTreatmentCommand>, RecordTreatmentCommandHandler>();
        services.AddScoped<IValidator<RecordTreatmentCommand>, RecordTreatmentCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ReverseTreatmentLineCommand>, ReverseTreatmentLineCommandHandler>();
        services.AddScoped<IValidator<ReverseTreatmentLineCommand>, ReverseTreatmentLineCommandValidator>();

        // Domain service (reads only) and the one SKU adapter (section 8.12 SHP-1 -- Shop published no ISkuReader).
        services.AddScoped<DispensaryBatchPicker>();
        services.AddScoped<IClinicalSkuReader, ClinicalSkuReader>();
        services.AddScoped<IVisitBillingReader, VisitBillingReader>(); // HO-BIL-001 Phase J: the typed read port Billing uses (owner decision 2026-10-14, backlog EMR 43)

        // Section 11.6 -- BusinessEvent audit row; never enqueues, never mutates a clinical aggregate.
        services.AddScoped<IEventHandlerAsync<TreatmentReversedEvent>, AuditTreatmentReversedHandler>();

        services.AddScoped<IQueryHandlerAsync<GetVisitChargeLinesQuery, VisitChargeLinesViewModel?>, GetVisitChargeLinesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDispensaryItemsQuery, PagedResult<DispensaryItemViewModel>>, GetDispensaryItemsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetIssuableBatchesQuery, IReadOnlyList<IssuableBatchViewModel>>, GetIssuableBatchesQueryHandler>();
    }

    private static void AddClinicalHistory(IServiceCollection services)
    {
        // §8.7 / ADR-128 -- the history is composed from section providers. A later area (HO-PHM-001 prescriptions,
        // HO-BRD-001 boarding stays, HO-ORD-001 food purchases) adds its panel by registering an
        // IClinicalHistorySectionProvider under its declared key; until then the summary reports available: false.
        // The concrete providers are registered as themselves (the timeline composes them) and as the interface.
        AddProvider<VisitsSectionProvider>(services);
        AddProvider<DiagnosesSectionProvider>(services);
        AddProvider<LabResultsSectionProvider>(services);
        AddProvider<PhotosSectionProvider>(services);
        AddProvider<VaccinationsSectionProvider>(services);
        AddProvider<ParasiteTreatmentsSectionProvider>(services);
        AddProvider<WeightsSectionProvider>(services);
        AddProvider<TimelineSectionProvider>(services);

        services.AddScoped<IQueryHandlerAsync<GetClinicalHistorySummaryQuery, ClinicalHistorySummaryViewModel?>, GetClinicalHistorySummaryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetClinicalHistorySectionQuery, ClinicalHistorySectionViewModel?>, GetClinicalHistorySectionQueryHandler>();
    }

    private static void AddProvider<TProvider>(IServiceCollection services) where TProvider : class, IClinicalHistorySectionProvider
    {
        services.AddScoped<TProvider>();
        services.AddScoped<IClinicalHistorySectionProvider>(sp => sp.GetRequiredService<TProvider>());
    }

    private static void AddSummaryAndFollowUp(IServiceCollection services)
    {
        // US-CLN-16 -- the discharge summary is derived on every read and printed with QuestPDF (Community licence,
        // owner-confirmed, HO-CAT-001 section 20.6). No IDischargeSummaryContributor is registered here: HO-PHM-001 adds one for
        // take-home prescriptions; until then the summary says prescriptionsAvailable: false.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        services.AddScoped<IDischargeSummaryPdfRenderer, QuestPdfDischargeSummaryRenderer>();

        services.AddScoped<IQueryHandlerAsync<GetDischargeSummaryQuery, DischargeSummaryViewModel?>, GetDischargeSummaryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerVisitSummaryQuery, DischargeSummaryViewModel?>, GetCustomerVisitSummaryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerCatVisitsQuery, IReadOnlyList<CustomerVisitListItemViewModel>?>, GetCustomerCatVisitsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerLabResultsQuery, IReadOnlyList<CustomerLabResultViewModel>?>, GetCustomerLabResultsQueryHandler>();

        // US-CLN-18 -- C18. The reminder is enqueued in the handler; its send-time guard is resolved by HO-NTF-001's
        // SubjectGuardResolver over every registered INotificationSubjectGuard.
        services.AddScoped<ICommandHandlerAsync<SetFollowUpCommand>, SetFollowUpCommandHandler>();
        services.AddScoped<IValidator<SetFollowUpCommand>, SetFollowUpCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ClearFollowUpCommand>, ClearFollowUpCommandHandler>();
        services.AddScoped<IValidator<ClearFollowUpCommand>, ClearFollowUpCommandValidator>();
        services.AddScoped<INotificationSubjectGuard, VisitFollowUpGuard>();
    }

    private static void AddConsents(IServiceCollection services)
    {
        // C19-C20 consent (US-CLN-21). The status query is HO-SRG-001's contract.
        services.AddScoped<ICommandHandlerAsync<RecordConsentCommand>, RecordConsentCommandHandler>();
        services.AddScoped<IValidator<RecordConsentCommand>, RecordConsentCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<WithdrawConsentCommand>, WithdrawConsentCommandHandler>();
        services.AddScoped<IValidator<WithdrawConsentCommand>, WithdrawConsentCommandValidator>();

        services.AddScoped<IQueryHandlerAsync<GetConsentFormQuery, ConsentFormViewModel?>, GetConsentFormQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetConsentQuery, ConsentViewModel?>, GetConsentQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCatConsentsQuery, IReadOnlyList<ConsentListItemViewModel>>, GetCatConsentsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetConsentStatusQuery, ConsentStatusViewModel>, GetConsentStatusQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetConsentSignatureQuery, string?>, GetConsentSignatureQueryHandler>();

        // §11.6 -- consent recorded and withdrawn are BusinessEvent audit rows (one class handles both events).
        services.AddScoped<IEventHandlerAsync<ConsentRecordedEvent>, AuditConsentHandler>();
        services.AddScoped<IEventHandlerAsync<ConsentWithdrawnEvent>, AuditConsentHandler>();
    }

    private static void AddRetention(IServiceCollection services)
    {
        // C-ALL-3: a clinical context provides its own retention-review count (a flag, never a delete -- ADR-301).
        // C-ALL-2: no IPersonalDataEraser -- the context is exempted in ErasureExemptions.Consultation (no customer id on any
        // visit row; a consent's signer name and signature are a Regulatory record, NFR-LEG-4 / D-8).
        services.AddScoped<IRetentionReviewSource, ConsultationRetentionReviewSource>();
    }

    private static void AddAccessLog(IServiceCollection services)
    {
        // C4 -- the clinical-record access log (NFR-SEC-8, ADR-129). The filter is resolved per action
        // through [ClinicalRecordAccess] (a TypeFilter), never registered globally.
        services.AddScoped<ICommandHandlerAsync<RecordClinicalRecordAccessCommand>, RecordClinicalRecordAccessCommandHandler>();
        services.AddScoped<IValidator<RecordClinicalRecordAccessCommand>, RecordClinicalRecordAccessCommandValidator>();
    }
}
