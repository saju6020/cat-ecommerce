// Billing module -- HO-BIL-001.
//
// Registers every command handler, validator (as IValidator<T> -- the platform resolves the interface),
// query handler, domain event handler and port this area needs. There is NO hosted service: nothing
// in Billing is timed (HO-BIL-001 §4.4, §20.8). Program.cs already calls AddBillingModule(...), so this
// file is the only one that changes per phase (ADR-341).

using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Billing;
using CatEcommerce.Infrastructure.Billing.Documents;
using CatEcommerce.Infrastructure.Billing.ItemSources;
using CatEcommerce.Application.DomainEventHandlers.Billing;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Events.Clinic;
using CatEcommerce.Domain.Events.Prescription;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Events.Visit;
using Platform.Infrastructure.Core.Events;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.QueryHandlers.Billing;
using CatEcommerce.Read.ViewModels;
using FluentValidation;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Api.Modules;

public static class BillingModule
{
    public static IServiceCollection AddBillingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // ── Ports (HO-BIL-001 §12.2) ─────────────────────────────────────────────────────────────
        services.AddScoped<IDocumentNumbering, DocumentNumbering>();
        services.AddScoped<IBillingTransactionScope, BillingTransactionScope>(); // HO-BIL-001 §8.13 (Amended 2026-10-09): the published producer entry point (ADR-215)

        services.AddScoped<IBillableChargeRecorder, EfBillableChargeRecorder>();
        services.AddScoped<IBillingPricingInputs, BillingPricingInputs>();
        services.AddScoped<IBillingFinder, EfBillingFinder>();
        services.AddScoped<IBillingRefundPlanner, EfBillingRefundPlanner>(); // HO-BIL-001 §8.13 (Amended 2026-10-09): the published refund planning port
        services.AddScoped<IBankMatchRecorder, EfBankMatchRecorder>();
        services.AddScoped<ILoyaltyAccountStore, EfLoyaltyAccountStore>();
        services.AddScoped<LoyaltyDependencies>();
        services.AddSingleton<IBankStatementCsvParser, BankStatementCsvParser>();
        services.AddScoped<IInvoicePdfRenderer, QuestPdfInvoiceRenderer>();
        services.AddScoped<ICreditNotePdfRenderer, QuestPdfCreditNoteRenderer>();

        // HO-XCT-001 §5.2 C-ALL-2/3 -- the bill-to snapshot on an issued invoice is personal data retained under D-9, so
        // Billing registers an eraser (it reports Held, mutates nothing) and a retention review source.
        services.AddScoped<IPersonalDataEraser, BillingPersonalDataEraser>();
        services.AddScoped<IRetentionReviewSource, BillingRetentionReviewSource>();

        // Item sources, keyed by ChargeSourceKind through the resolver (§12.7). Phase J (owner decisions 2026-10-14) adds Visit, Dispense and Surgery.
        services.AddScoped<IBillableItemSource, AppointmentBillableItemSource>();
        services.AddScoped<IBillableItemSource, VisitBillableItemSource>();
        services.AddScoped<IBillableItemSource, DispenseBillableItemSource>();
        services.AddScoped<IBillableItemSource, SurgeryBillableItemSource>();
        services.AddScoped<IBillableItemSourceResolver, BillableItemSourceResolver>();


        // HO-BIL-001 §12.5 / ADR-187 -- the parallel double-submit of a money-moving write is answered with the original result.
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options => options.Filters.Add<CatEcommerce.Api.Filters.BillingIdempotencyReplayFilter>());

        RegisterCommands(services);
        RegisterQueries(services);
        RegisterDomainEventHandlers(services);

        return services;
    }

    private static void RegisterCommands(IServiceCollection services)
    {
        services.AddScoped<ICommandHandlerAsync<SetBillingProfileCommand>, SetBillingProfileCommandHandler>();
        services.AddScoped<IValidator<SetBillingProfileCommand>, SetBillingProfileCommandValidator>();

        // Phase B -- charges and capture
        services.AddScoped<ICommandHandlerAsync<RecordManualChargeCommand>, RecordManualChargeCommandHandler>();
        services.AddScoped<IValidator<RecordManualChargeCommand>, RecordManualChargeCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<VoidChargeCommand>, VoidChargeCommandHandler>();
        services.AddScoped<IValidator<VoidChargeCommand>, VoidChargeCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CaptureSourceChargesCommand>, CaptureSourceChargesCommandHandler>();
        services.AddScoped<IValidator<CaptureSourceChargesCommand>, CaptureSourceChargesCommandValidator>();

        // Phase C -- invoices
        services.AddScoped<ICommandHandlerAsync<CreateDraftInvoiceCommand>, CreateDraftInvoiceCommandHandler>();
        services.AddScoped<IValidator<CreateDraftInvoiceCommand>, CreateDraftInvoiceCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SetDraftInvoiceChargesCommand>, SetDraftInvoiceChargesCommandHandler>();
        services.AddScoped<IValidator<SetDraftInvoiceChargesCommand>, SetDraftInvoiceChargesCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<DiscardDraftInvoiceCommand>, DiscardDraftInvoiceCommandHandler>();
        services.AddScoped<IValidator<DiscardDraftInvoiceCommand>, DiscardDraftInvoiceCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<IssueInvoiceCommand>, IssueInvoiceCommandHandler>();
        services.AddScoped<IValidator<IssueInvoiceCommand>, IssueInvoiceCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<IssueInvoiceFromChargesCommand>, IssueInvoiceFromChargesCommandHandler>();
        services.AddScoped<IValidator<IssueInvoiceFromChargesCommand>, IssueInvoiceFromChargesCommandValidator>();

        // Phase D -- payments
        services.AddScoped<ICommandHandlerAsync<RecordPaymentCommand>, RecordPaymentCommandHandler>();
        services.AddScoped<IValidator<RecordPaymentCommand>, RecordPaymentCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ApprovePaymentCommand>, ApprovePaymentCommandHandler>();
        services.AddScoped<IValidator<ApprovePaymentCommand>, ApprovePaymentCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RejectPaymentCommand>, RejectPaymentCommandHandler>();
        services.AddScoped<IValidator<RejectPaymentCommand>, RejectPaymentCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<AllocatePaymentCommand>, AllocatePaymentCommandHandler>();
        services.AddScoped<IValidator<AllocatePaymentCommand>, AllocatePaymentCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ReleasePaymentEarmarkCommand>, ReleasePaymentEarmarkCommandHandler>();
        services.AddScoped<IValidator<ReleasePaymentEarmarkCommand>, ReleasePaymentEarmarkCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ReversePaymentCommand>, ReversePaymentCommandHandler>();
        services.AddScoped<IValidator<ReversePaymentCommand>, ReversePaymentCommandValidator>();

        // Phase E -- credit notes, corrected re-issue, refunds
        services.AddScoped<ICommandHandlerAsync<IssueCreditNoteCommand>, IssueCreditNoteCommandHandler>();
        services.AddScoped<IValidator<IssueCreditNoteCommand>, IssueCreditNoteCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CreateCorrectedDraftCommand>, CreateCorrectedDraftCommandHandler>();
        services.AddScoped<IValidator<CreateCorrectedDraftCommand>, CreateCorrectedDraftCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<SetCorrectedDraftLinesCommand>, SetCorrectedDraftLinesCommandHandler>();
        services.AddScoped<IValidator<SetCorrectedDraftLinesCommand>, SetCorrectedDraftLinesCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<IssueRefundCommand>, IssueRefundCommandHandler>();
        services.AddScoped<IValidator<IssueRefundCommand>, IssueRefundCommandValidator>();

        // Phase F -- cash-up
        services.AddScoped<ICommandHandlerAsync<RecordCashCountCommand>, RecordCashCountCommandHandler>();
        services.AddScoped<IValidator<RecordCashCountCommand>, RecordCashCountCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CloseCashUpCommand>, CloseCashUpCommandHandler>();
        services.AddScoped<IValidator<CloseCashUpCommand>, CloseCashUpCommandValidator>();

        // Phase G -- reconciliation
        services.AddScoped<ICommandHandlerAsync<RecordCodRemittanceCommand>, RecordCodRemittanceCommandHandler>();
        services.AddScoped<IValidator<RecordCodRemittanceCommand>, RecordCodRemittanceCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ImportBankStatementCommand>, ImportBankStatementCommandHandler>();
        services.AddScoped<IValidator<ImportBankStatementCommand>, ImportBankStatementCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<AutoMatchBankStatementCommand>, AutoMatchBankStatementCommandHandler>();
        services.AddScoped<IValidator<AutoMatchBankStatementCommand>, AutoMatchBankStatementCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<MatchBankLineCommand>, MatchBankLineCommandHandler>();
        services.AddScoped<IValidator<MatchBankLineCommand>, MatchBankLineCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<UnmatchBankLineCommand>, UnmatchBankLineCommandHandler>();
        services.AddScoped<IValidator<UnmatchBankLineCommand>, UnmatchBankLineCommandValidator>();

        // Phase H -- accounting export
        services.AddScoped<ICommandHandlerAsync<RecordAccountingExportRunCommand>, RecordAccountingExportRunCommandHandler>();
        services.AddScoped<IValidator<RecordAccountingExportRunCommand>, RecordAccountingExportRunCommandValidator>();

        // Phase I -- loyalty and customer credit
        services.AddScoped<ICommandHandlerAsync<ApplyLoyaltyRedemptionCommand>, ApplyLoyaltyRedemptionCommandHandler>();
        services.AddScoped<IValidator<ApplyLoyaltyRedemptionCommand>, ApplyLoyaltyRedemptionCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RemoveLoyaltyRedemptionCommand>, RemoveLoyaltyRedemptionCommandHandler>();
        services.AddScoped<IValidator<RemoveLoyaltyRedemptionCommand>, RemoveLoyaltyRedemptionCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ApplyCustomerCreditCommand>, ApplyCustomerCreditCommandHandler>();
        services.AddScoped<IValidator<ApplyCustomerCreditCommand>, ApplyCustomerCreditCommandValidator>();
    }

    private static void RegisterQueries(IServiceCollection services)
    {
        services.AddScoped<IQueryHandlerAsync<GetPaymentMethodsQuery, IReadOnlyList<PaymentMethodViewModel>>, GetPaymentMethodsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBillingProfileQuery, BillingProfileViewModel?>, GetBillingProfileQueryHandler>();

        // Phase B
        services.AddScoped<IQueryHandlerAsync<GetChargesQuery, PagedResult<ChargeViewModel>>, GetChargesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetUncapturedSourcesQuery, IReadOnlyList<UncapturedSourceViewModel>>, GetUncapturedSourcesQueryHandler>();

        // Phase C
        services.AddScoped<IQueryHandlerAsync<GetInvoicesQuery, PagedResult<InvoiceSummaryViewModel>>, GetInvoicesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetInvoiceDetailQuery, InvoiceDetailViewModel?>, GetInvoiceDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<PreviewInvoiceQuery, InvoicePreviewViewModel>, PreviewInvoiceQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetInvoiceDocumentQuery, InvoiceDocument?>, GetInvoiceDocumentQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerInvoicesQuery, PagedResult<InvoiceSummaryViewModel>>, GetCustomerInvoicesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerInvoiceDetailQuery, CustomerInvoiceDetailViewModel?>, GetCustomerInvoiceDetailQueryHandler>();

        // Phase D -- payments, receivables and the published read contracts (§8.13)
        services.AddScoped<IQueryHandlerAsync<GetPaymentsQuery, PagedResult<PaymentSummaryViewModel>>, GetPaymentsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPaymentDetailQuery, PaymentDetailViewModel?>, GetPaymentDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPaymentReceiptQuery, PaymentReceiptViewModel?>, GetPaymentReceiptQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPendingApprovalsQuery, IReadOnlyList<PendingApprovalViewModel>>, GetPendingApprovalsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPaymentRecordResultQuery, PaymentRecordResultViewModel?>, GetPaymentRecordResultQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerPaymentsQuery, PagedResult<PaymentSummaryViewModel>>, GetCustomerPaymentsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerBillingSummaryQuery, CustomerBillingSummaryViewModel>, GetCustomerBillingSummaryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetAgeingQuery, AgeingViewModel>, GetAgeingQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBillingGroupSummaryQuery, BillingGroupSummaryViewModel>, GetBillingGroupSummaryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetSourceBillingStatusQuery, SourceBillingStatusViewModel>, GetSourceBillingStatusQueryHandler>();

        // Phase E
        services.AddScoped<IQueryHandlerAsync<GetCreditNoteDetailQuery, CreditNoteDetailViewModel?>, GetCreditNoteDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCreditNoteDocumentQuery, CreditNoteDocument?>, GetCreditNoteDocumentQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRefundsQuery, PagedResult<RefundListItemViewModel>>, GetRefundsQueryHandler>();

        // Phase F
        services.AddScoped<IQueryHandlerAsync<GetCashUpQuery, CashUpViewModel>, GetCashUpQueryHandler>();

        // Phase G
        services.AddScoped<IQueryHandlerAsync<GetCodCollectionsQuery, PagedResult<CodCollectionViewModel>>, GetCodCollectionsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCodRemittancesQuery, PagedResult<CodRemittanceViewModel>>, GetCodRemittancesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCodReconciliationQuery, CodReconciliationViewModel>, GetCodReconciliationQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBankStatementsQuery, PagedResult<BankStatementSummaryViewModel>>, GetBankStatementsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBankStatementReconciliationQuery, BankStatementReconciliationViewModel?>, GetBankStatementReconciliationQueryHandler>();

        // Phase H
        services.AddScoped<IQueryHandlerAsync<GetAccountingExportContentQuery, CatEcommerce.Infrastructure.Billing.Export.AccountingExportContent>, GetAccountingExportContentQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetAccountingExportRunsQuery, PagedResult<AccountingExportRunViewModel>>, GetAccountingExportRunsQueryHandler>();

        // Phase I
        services.AddScoped<IQueryHandlerAsync<GetLoyaltyQuery, LoyaltyViewModel>, GetLoyaltyQueryHandler>();
    }

    private static void RegisterDomainEventHandlers(IServiceCollection services)
    {
        // Phase B -- thin capture handler (fresh scope, swallow and log); never enqueues.
        services.AddScoped<IEventHandlerAsync<AppointmentCompletedEvent>, CaptureChargesOnAppointmentCompletedHandler>();

        // Phase J -- the same thin shape for the three areas merged since; the reversal handler voids what was not yet invoiced.
        services.AddScoped<IEventHandlerAsync<VisitFinalisedEvent>, CaptureChargesOnVisitFinalisedHandler>();
        services.AddScoped<IEventHandlerAsync<PrescriptionDispensedEvent>, CaptureChargesOnPrescriptionDispensedHandler>();
        services.AddScoped<IEventHandlerAsync<SurgeryDischargedEvent>, CaptureChargesOnSurgeryDischargedHandler>();
        services.AddScoped<IEventHandlerAsync<DispensingReversedEvent>, VoidChargesOnDispensingReversedHandler>();

        // Phase C -- the merge is binding on every context holding a customer id (HO-ACC-001 ADR-039).
        services.AddScoped<IEventHandlerAsync<CustomerMergedEvent>, ReassignBillingOnCustomerMergedHandler>();

        // Phase D -- the decision trail: the reason lives here, not on the event (§11.5, §11.6).
        services.AddScoped<IEventHandlerAsync<PaymentApprovalWaivedEvent>, AuditBillingDecisionHandler>();
        services.AddScoped<IEventHandlerAsync<PaymentApprovedEvent>, AuditBillingDecisionHandler>();
        services.AddScoped<IEventHandlerAsync<PaymentRejectedEvent>, AuditBillingDecisionHandler>();
        services.AddScoped<IEventHandlerAsync<PaymentReversedEvent>, AuditBillingDecisionHandler>();
        services.AddScoped<IEventHandlerAsync<PaymentEarmarkReleasedEvent>, AuditBillingDecisionHandler>();
        services.AddScoped<IEventHandlerAsync<CreditNoteIssuedEvent>, AuditBillingDecisionHandler>();
        services.AddScoped<IEventHandlerAsync<RefundIssuedEvent>, AuditBillingDecisionHandler>();
        services.AddScoped<IEventHandlerAsync<CashUpClosedEvent>, AuditBillingDecisionHandler>();
        services.AddScoped<IEventHandlerAsync<BankLineUnmatchedEvent>, AuditBillingDecisionHandler>();
        services.AddScoped<IEventHandlerAsync<AccountingExportRecordedEvent>, AuditBillingDecisionHandler>();

        // Phase I -- points accrue on a paid invoice and are taken back by a credit note; both mutate Billing roots only.
        services.AddScoped<ReverseLoyaltyOnCreditNoteIssuedHandler>();
        services.AddScoped<IEventHandlerAsync<CreditNoteIssuedEvent>>(sp => sp.GetRequiredService<ReverseLoyaltyOnCreditNoteIssuedHandler>());
        services.AddScoped<IEventHandlerAsync<InvoiceStatusChangedEvent>, AccrueLoyaltyOnInvoicePaidHandler>();
    }
}
