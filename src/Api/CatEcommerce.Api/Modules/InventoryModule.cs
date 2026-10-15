// Inventory module -- HO-IMP-001.
//
// Registers every command handler, validator (as IValidator<T> -- the platform resolves the interface, a validator registered as its
// base class silently never runs), query handler and port this area needs. Program.cs already calls AddInventoryModule(configuration)
// once (ADR-341) -- this file is the only one that changes as each phase lands. Inventory has NO domain event handlers (§11.6): every
// side effect of a stock change happens in the command, in the same commit.

using CatEcommerce.Application.CommandHandlers.Inventory;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Api.Background;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Inventory;
using CatEcommerce.Infrastructure.Notifications.Guards;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Read.QueryHandlers.Inventory;
using CatEcommerce.Read.ViewModels;
using FluentValidation;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Api.Modules;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Ports (§8.14). Each on its own line.
        services.AddScoped<ISkuDirectory, CatalogueSkuDirectory>();
        services.AddScoped<IInventoryClock, InventoryClock>();
        services.AddScoped<IDocumentNumberGenerator, SequenceDocumentNumberGenerator>();
        services.AddScoped<IStockPositionReader, EfStockPositionReader>();
        services.AddScoped<IBatchRegistry, EfBatchRegistry>();
        services.AddScoped<IInventoryAlertEnlister, EfInventoryAlertEnlister>();
        services.AddScoped<IInventoryAuditEnlister, EfInventoryAuditEnlister>();
        services.AddScoped<IInventoryAlertWriter, EfInventoryAlertWriter>();
        services.AddScoped<IRetentionReviewSource, InventoryRetentionReviewSource>();
        services.AddScoped<IRecallRecipientResolver, RecallRecipientResolver>();

        // Valid while the batch is Recalled (section 8.12); resolved by HO-NTF-001's SubjectGuardResolver with the other guards.
        services.AddScoped<INotificationSubjectGuard, BatchRecallGuard>();

        // ONE Scoped EfStockLedger serves BOTH stock ports (§12.7, ADR-103), so a unit of work shares one view of the rows it has enlisted. This
        // replaces the contracts pass's fail-closed FakeStockLedger registration (docs/handover/CONTRACTS.md #1).
        services.AddScoped<EfStockLedger>();
        services.AddScoped<IInventoryLedgerWriter>(sp => sp.GetRequiredService<EfStockLedger>());
        services.AddScoped<IStockLedger>(sp => sp.GetRequiredService<EfStockLedger>());

        // The pure domain services (§8.14) -- no state, no I/O.
        services.AddScoped<FefoAllocator>();
        services.AddScoped<StockDemandPlanner>();
        services.AddScoped<ReorderSuggestionCalculator>();

        // The alert worker (section 12.2): options bound from "Inventory", one scoped work cycle, one hosted service.
        services.AddOptions<InventoryOptions>()
            .BindConfiguration(InventoryOptions.SectionName)
            .Validate(o => o.Worker.IntervalSeconds >= 1, "Inventory:Worker:IntervalSeconds must be at least 1.")
            .ValidateOnStart();
        services.AddSingleton<InventoryWorkerState>();
        services.AddScoped<InventoryWorkCycle>();
        services.AddHostedService<InventoryWorker>();

        RegisterCommands(services);
        RegisterQueries(services);

        return services;
    }

    private static void RegisterCommands(IServiceCollection services)
    {
        // ── Phase A: locations and storage profiles (C1, C2). ──
        services.AddScoped<ICommandHandlerAsync<CreateStockLocationCommand>, CreateStockLocationCommandHandler>();
        services.AddScoped<IValidator<CreateStockLocationCommand>, CreateStockLocationCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateStockLocationCommand>, UpdateStockLocationCommandHandler>();
        services.AddScoped<IValidator<UpdateStockLocationCommand>, UpdateStockLocationCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetStockLocationActivationCommand>, SetStockLocationActivationCommandHandler>();
        services.AddScoped<IValidator<SetStockLocationActivationCommand>, SetStockLocationActivationCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetProductStorageProfileCommand>, SetProductStorageProfileCommandHandler>();
        services.AddScoped<IValidator<SetProductStorageProfileCommand>, SetProductStorageProfileCommandValidator>();

        // ── Phase H. ──
        services.AddScoped<ICommandHandlerAsync<SetReorderPolicyCommand>, SetReorderPolicyCommandHandler>();
        services.AddScoped<IValidator<SetReorderPolicyCommand>, SetReorderPolicyCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RaiseStockAlertCommand>, RaiseStockAlertCommandHandler>();
        services.AddScoped<IValidator<RaiseStockAlertCommand>, RaiseStockAlertCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<ResolveStockAlertCommand>, ResolveStockAlertCommandHandler>();
        services.AddScoped<IValidator<ResolveStockAlertCommand>, ResolveStockAlertCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<AcknowledgeStockAlertCommand>, AcknowledgeStockAlertCommandHandler>();
        services.AddScoped<IValidator<AcknowledgeStockAlertCommand>, AcknowledgeStockAlertCommandValidator>();

        // ── Phase I. ──
        services.AddScoped<ICommandHandlerAsync<RecallBatchCommand>, RecallBatchCommandHandler>();
        services.AddScoped<IValidator<RecallBatchCommand>, RecallBatchCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<NotifyRecallRecipientsCommand>, NotifyRecallRecipientsCommandHandler>();
        services.AddScoped<IValidator<NotifyRecallRecipientsCommand>, NotifyRecallRecipientsCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<WithdrawBatchRecallCommand>, WithdrawBatchRecallCommandHandler>();
        services.AddScoped<IValidator<WithdrawBatchRecallCommand>, WithdrawBatchRecallCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CloseBatchRecallCommand>, CloseBatchRecallCommandHandler>();
        services.AddScoped<IValidator<CloseBatchRecallCommand>, CloseBatchRecallCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<RecordRecallTraceExportCommand>, RecordRecallTraceExportCommandHandler>();
        services.AddScoped<IValidator<RecordRecallTraceExportCommand>, RecordRecallTraceExportCommandValidator>();

        // ── Phase J. ──
        services.AddScoped<ICommandHandlerAsync<ReportStorageBreachCommand>, ReportStorageBreachCommandHandler>();
        services.AddScoped<IValidator<ReportStorageBreachCommand>, ReportStorageBreachCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<QuarantineStorageBreachStockCommand>, QuarantineStorageBreachStockCommandHandler>();
        services.AddScoped<IValidator<QuarantineStorageBreachStockCommand>, QuarantineStorageBreachStockCommandValidator>();
        services.AddScoped<ICommandHandlerAsync<CloseStorageBreachCommand>, CloseStorageBreachCommandHandler>();
        services.AddScoped<IValidator<CloseStorageBreachCommand>, CloseStorageBreachCommandValidator>();

        // ── Phase B: suppliers (C3) and purchase orders (C4). ──
        services.AddScoped<ICommandHandlerAsync<CreateSupplierCommand>, CreateSupplierCommandHandler>();
        services.AddScoped<IValidator<CreateSupplierCommand>, CreateSupplierCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateSupplierCommand>, UpdateSupplierCommandHandler>();
        services.AddScoped<IValidator<UpdateSupplierCommand>, UpdateSupplierCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetSupplierActivationCommand>, SetSupplierActivationCommandHandler>();
        services.AddScoped<IValidator<SetSupplierActivationCommand>, SetSupplierActivationCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CreatePurchaseOrderCommand>, CreatePurchaseOrderCommandHandler>();
        services.AddScoped<IValidator<CreatePurchaseOrderCommand>, CreatePurchaseOrderCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ReplacePurchaseOrderDraftCommand>, ReplacePurchaseOrderDraftCommandHandler>();
        services.AddScoped<IValidator<ReplacePurchaseOrderDraftCommand>, ReplacePurchaseOrderDraftCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<IssuePurchaseOrderCommand>, IssuePurchaseOrderCommandHandler>();
        services.AddScoped<IValidator<IssuePurchaseOrderCommand>, IssuePurchaseOrderCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CancelPurchaseOrderCommand>, CancelPurchaseOrderCommandHandler>();
        services.AddScoped<IValidator<CancelPurchaseOrderCommand>, CancelPurchaseOrderCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ClosePurchaseOrderCommand>, ClosePurchaseOrderCommandHandler>();
        services.AddScoped<IValidator<ClosePurchaseOrderCommand>, ClosePurchaseOrderCommandValidator>();

        // ── Phase C: consignments (C5, C6, C7). ──
        services.AddScoped<ICommandHandlerAsync<PlanConsignmentCommand>, PlanConsignmentCommandHandler>();
        services.AddScoped<IValidator<PlanConsignmentCommand>, PlanConsignmentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateConsignmentDetailsCommand>, UpdateConsignmentDetailsCommandHandler>();
        services.AddScoped<IValidator<UpdateConsignmentDetailsCommand>, UpdateConsignmentDetailsCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<AdvanceConsignmentStatusCommand>, AdvanceConsignmentStatusCommandHandler>();
        services.AddScoped<IValidator<AdvanceConsignmentStatusCommand>, AdvanceConsignmentStatusCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CancelConsignmentCommand>, CancelConsignmentCommandHandler>();
        services.AddScoped<IValidator<CancelConsignmentCommand>, CancelConsignmentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<AddConsignmentDocumentCommand>, AddConsignmentDocumentCommandHandler>();
        services.AddScoped<IValidator<AddConsignmentDocumentCommand>, AddConsignmentDocumentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SupersedeConsignmentDocumentCommand>, SupersedeConsignmentDocumentCommandHandler>();
        services.AddScoped<IValidator<SupersedeConsignmentDocumentCommand>, SupersedeConsignmentDocumentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ReplaceConsignmentCostsCommand>, ReplaceConsignmentCostsCommandHandler>();
        services.AddScoped<ICommandHandlerAsync<AllocateLandedCostCommand>, AllocateLandedCostCommandHandler>();
        services.AddScoped<IValidator<AllocateLandedCostCommand>, AllocateLandedCostCommandValidator>();
        services.AddScoped<LandedCostAllocator>();
        services.AddScoped<IValidator<ReplaceConsignmentCostsCommand>, ReplaceConsignmentCostsCommandValidator>();

        // ── Phase D: goods receipt (C8) and batch status (C9). ──
        services.AddScoped<ICommandHandlerAsync<PostGoodsReceiptCommand>, PostGoodsReceiptCommandHandler>();
        services.AddScoped<IValidator<PostGoodsReceiptCommand>, PostGoodsReceiptCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<QuarantineBatchCommand>, QuarantineBatchCommandHandler>();
        services.AddScoped<IValidator<QuarantineBatchCommand>, QuarantineBatchCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ReleaseBatchCommand>, ReleaseBatchCommandHandler>();
        services.AddScoped<IValidator<ReleaseBatchCommand>, ReleaseBatchCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<WriteOffBatchCommand>, WriteOffBatchCommandHandler>();
        services.AddScoped<IValidator<WriteOffBatchCommand>, WriteOffBatchCommandValidator>();

        // ── Phase F: transfers (C10), adjustments (C11) and counts (C12). ──
        services.AddScoped<ICommandHandlerAsync<PostStockTransferCommand>, PostStockTransferCommandHandler>();
        services.AddScoped<IValidator<PostStockTransferCommand>, PostStockTransferCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RequestStockAdjustmentCommand>, RequestStockAdjustmentCommandHandler>();
        services.AddScoped<IValidator<RequestStockAdjustmentCommand>, RequestStockAdjustmentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ApproveStockAdjustmentCommand>, ApproveStockAdjustmentCommandHandler>();
        services.AddScoped<IValidator<ApproveStockAdjustmentCommand>, ApproveStockAdjustmentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RejectStockAdjustmentCommand>, RejectStockAdjustmentCommandHandler>();
        services.AddScoped<IValidator<RejectStockAdjustmentCommand>, RejectStockAdjustmentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<WithdrawStockAdjustmentCommand>, WithdrawStockAdjustmentCommandHandler>();
        services.AddScoped<IValidator<WithdrawStockAdjustmentCommand>, WithdrawStockAdjustmentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<OpenStockCountCommand>, OpenStockCountCommandHandler>();
        services.AddScoped<IValidator<OpenStockCountCommand>, OpenStockCountCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordCountLinesCommand>, RecordCountLinesCommandHandler>();
        services.AddScoped<IValidator<RecordCountLinesCommand>, RecordCountLinesCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SubmitStockCountCommand>, SubmitStockCountCommandHandler>();
        services.AddScoped<IValidator<SubmitStockCountCommand>, SubmitStockCountCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CommitStockCountCommand>, CommitStockCountCommandHandler>();
        services.AddScoped<IValidator<CommitStockCountCommand>, CommitStockCountCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ApproveStockCountCommand>, ApproveStockCountCommandHandler>();
        services.AddScoped<IValidator<ApproveStockCountCommand>, ApproveStockCountCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RejectStockCountCommand>, RejectStockCountCommandHandler>();
        services.AddScoped<IValidator<RejectStockCountCommand>, RejectStockCountCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CancelStockCountCommand>, CancelStockCountCommandHandler>();
        services.AddScoped<IValidator<CancelStockCountCommand>, CancelStockCountCommandValidator>();
    }

    private static void RegisterQueries(IServiceCollection services)
    {
        // ── Phase A. ──
        services.AddScoped<IQueryHandlerAsync<GetStockLocationsQuery, IReadOnlyList<StockLocationViewModel>>, GetStockLocationsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetSkuLookupQuery, IReadOnlyList<SkuLookupViewModel>>, GetSkuLookupQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStorageProfileQuery, StorageProfileViewModel?>, GetStorageProfileQueryHandler>();

        // ── Phase B. ──
        services.AddScoped<IQueryHandlerAsync<GetSuppliersQuery, PagedResult<SupplierSummaryViewModel>>, GetSuppliersQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetSupplierQuery, SupplierDetailViewModel?>, GetSupplierQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPurchaseOrdersQuery, PagedResult<PurchaseOrderSummaryViewModel>>, GetPurchaseOrdersQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPurchaseOrderQuery, PurchaseOrderDetailViewModel?>, GetPurchaseOrderQueryHandler>();

        // ── Phase C. ──
        services.AddScoped<IQueryHandlerAsync<GetConsignmentsQuery, PagedResult<ConsignmentSummaryViewModel>>, GetConsignmentsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetConsignmentQuery, ConsignmentDetailViewModel?>, GetConsignmentQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetInspectionPackQuery, InspectionPackViewModel?>, GetInspectionPackQueryHandler>();

        // ── Phase D. ──
        services.AddScoped<IQueryHandlerAsync<GetGoodsReceiptsQuery, PagedResult<GoodsReceiptSummaryViewModel>>, GetGoodsReceiptsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetGoodsReceiptQuery, GoodsReceiptDetailViewModel?>, GetGoodsReceiptQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStockSummaryQuery, PagedResult<StockSummaryViewModel>>, GetStockSummaryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetSkuStockMatrixQuery, SkuStockMatrixViewModel?>, GetSkuStockMatrixQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBatchesQuery, PagedResult<BatchSummaryViewModel>>, GetBatchesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBatchQuery, BatchDetailViewModel?>, GetBatchQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetLedgerQuery, PagedResult<LedgerEntryViewModel>>, GetLedgerQueryHandler>();

        // ── Phase E: the published availability contract (ADR-116) and the FEFO preview. ──
        services.AddScoped<IQueryHandlerAsync<GetSellableStockQuery, IReadOnlyList<SellableStockViewModel>>, GetSellableStockQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetFefoPreviewQuery, FefoPreviewViewModel>, GetFefoPreviewQueryHandler>();

        // ── Phase F. ──
        services.AddScoped<IQueryHandlerAsync<GetStockTransfersQuery, PagedResult<StockTransferSummaryViewModel>>, GetStockTransfersQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStockTransferQuery, StockTransferDetailViewModel?>, GetStockTransferQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStockAdjustmentsQuery, PagedResult<StockAdjustmentSummaryViewModel>>, GetStockAdjustmentsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStockAdjustmentQuery, StockAdjustmentDetailViewModel?>, GetStockAdjustmentQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStockCountsQuery, PagedResult<StockCountSummaryViewModel>>, GetStockCountsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStockCountSheetQuery, StockCountDetailViewModel?>, GetStockCountSheetQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStockCountVarianceQuery, StockCountVarianceViewModel?>, GetStockCountVarianceQueryHandler>();

        // ── Phase H. ──
        services.AddScoped<IQueryHandlerAsync<GetExpiryAlertCandidatesQuery, IReadOnlyList<ExpiryAlertCandidate>>, GetExpiryAlertCandidatesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetLowStockCandidatesQuery, IReadOnlyList<LowStockCandidate>>, GetLowStockCandidatesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRecoveredLowStockAlertsQuery, IReadOnlyList<Guid>>, GetRecoveredLowStockAlertsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStockAlertsQuery, PagedResult<InventoryAlertViewModel>>, GetStockAlertsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStockAlertSummaryQuery, StockAlertSummaryViewModel>, GetStockAlertSummaryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetReorderPoliciesQuery, PagedResult<ReorderPolicyViewModel>>, GetReorderPoliciesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetReorderSuggestionsQuery, IReadOnlyList<ReorderSuggestionViewModel>>, GetReorderSuggestionsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetNearExpiryReportQuery, NearExpiryReportViewModel>, GetNearExpiryReportQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStockOnHandReportQuery, StockOnHandReportViewModel>, GetStockOnHandReportQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStockReconciliationQuery, IReadOnlyList<StockReconciliationRowViewModel>>, GetStockReconciliationQueryHandler>();

        // ── Phase I. ──
        services.AddScoped<IQueryHandlerAsync<GetRecallsQuery, PagedResult<RecallSummaryViewModel>>, GetRecallsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRecallQuery, RecallDetailViewModel?>, GetRecallQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRecallTraceQuery, RecallTraceViewModel?>, GetRecallTraceQueryHandler>();

        // ── Phase J. ──
        services.AddScoped<IQueryHandlerAsync<GetStorageBreachesQuery, PagedResult<StorageBreachSummaryViewModel>>, GetStorageBreachesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStorageBreachQuery, StorageBreachDetailViewModel?>, GetStorageBreachQueryHandler>();
    }
}
