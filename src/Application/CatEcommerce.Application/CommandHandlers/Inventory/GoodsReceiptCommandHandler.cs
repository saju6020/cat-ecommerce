using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Inventory;

/// <summary>What a posted receipt returns: its number and the batch each accepted line became.</summary>
public sealed record PostedGoodsReceipt(string Number, IReadOnlyList<Guid> BatchIds);

/// <summary>
/// HO-IMP-001 §8.6 / §11.4 C8 / ADR-105 -- posts a goods receipt in ONE commit: the receipt, one batch per accepted line (enlisted through
/// <see cref="IBatchRegistry"/>) and one <c>Receipt</c> ledger entry per batch (enlisted through <see cref="IInventoryLedgerWriter"/>), then the single
/// <c>SaveAsync</c> -- a recorded rule-1 exception, because a receipt half-posted is stock without a batch or a batch without stock.
///
/// The handler READS everything the rules need (PO and its prior receipts, consignment, SKUs, locations, the branch's today, the base currency),
/// then <c>GoodsReceiptAggregate.Validate</c> decides -- collecting EVERY line violation (<c>Lines[i].ExpiresOn</c>, ...) so the receiving screen
/// shows all of them at once; the aggregate's <c>Post</c> validates again so the invariants hold whoever calls it (rule 20). Header rules that need
/// other aggregates (PO issued, supplier match, consignment cleared) are decided here.
/// </summary>
public sealed class PostGoodsReceiptCommandHandler(
    ILogger<PostGoodsReceiptCommandHandler> logger,
    IAggregateRootRepository<GoodsReceiptAggregate> receipts,
    IAggregateRootRepository<PurchaseOrderAggregate> orders,
    IAggregateRootRepository<SupplierAggregate> suppliers,
    IAggregateRootRepository<ConsignmentAggregate> consignments,
    ISkuDirectory skus,
    IStockPositionReader positions,
    IInventoryClock clock,
    IBatchRegistry batchRegistry,
    IInventoryLedgerWriter ledgerWriter,
    IDocumentNumberGenerator numbers,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<PostGoodsReceiptCommand>
{
    public async Task<CommandResponse> HandleAsync(PostGoodsReceiptCommand command)
    {
        logger.LogInformation("Posting goods receipt {GoodsReceiptId} with {LineCount} lines. CorrelationId: {CorrelationId}",
            command.GoodsReceiptId, command.Lines.Count, command.CorrelationId);

        var header = await ResolveHeaderAsync(command);
        if (header.Failure is not null) return header.Failure;

        var skuProfiles = (await skus.GetManyAsync(command.Lines.Select(l => l.SkuId).Distinct().ToList(), CancellationToken.None)).ToDictionary(s => s.SkuId);
        var locations = await positions.GetLocationsAsync(command.Lines.Select(l => l.LocationId).Distinct().ToList(), CancellationToken.None);
        var context = new GoodsReceiptValidationContext(
            await clock.LocalTodayAsync(command.BranchId, CancellationToken.None),
            settings.GetString(SettingKeys.CommerceCurrencyCode, command.BranchId),
            skuProfiles, locations, await PurchaseOrderLineFactsAsync(header.Order), header.Consignment is not null,
            header.Consignment is null ? new Dictionary<string, decimal>() : header.Consignment.ExchangeRates.ToDictionary(r => r.CurrencyCode, r => r.RateToBase));

        var now = timeProvider.GetUtcNow();
        var dto = new GoodsReceiptPostDto(
            await numbers.NextAsync(DocumentKind.GoodsReceipt, CancellationToken.None), command.BranchId, header.SupplierId, header.Order?.Id, header.Consignment?.Id,
            command.DeliveryReference, now, command.Lines.Select(l => l.ToDomainDto()).ToList(), command.ToActor());

        var violations = GoodsReceiptAggregate.Validate(dto, context);
        if (violations.Count > 0) return InventoryResponses.Fail(violations);

        var receipt = new GoodsReceiptAggregate(command.GoodsReceiptId);
        try { receipt.Post(dto, context); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        var drafts = new List<StockMovementDraft>();
        var batchIds = new List<Guid>();
        foreach (var (line, input) in receipt.Lines.Zip(dto.Lines))
        {
            if (line.BatchId is not { } batchId) continue; // nothing accepted: recorded on the line, never in stock

            var cost = GoodsReceiptAggregate.ResolveCost(input, context)!.Value;
            var batch = new BatchAggregate(batchId);
            batch.Create(new NewBatchDto(
                batchId, command.BranchId, receipt.Id, line.Id, header.Consignment?.Id, header.SupplierId, line.SkuId, skuProfiles[line.SkuId].Snapshot, line.LotNumber,
                line.ManufacturedOn, line.ExpiresOn, header.Consignment?.OriginCountryCode ?? skuProfiles[line.SkuId].CountryOfOriginCode, now, line.QuantityAccepted,
                cost.Amount, cost.Currency, cost.Base, line.HoldForInspection ? BatchStatus.Quarantined : BatchStatus.Available, command.ToActor()));

            batchRegistry.Enlist(batch);
            batchIds.Add(batchId);
            drafts.Add(new StockMovementDraft(
                command.BranchId, line.LocationId, line.SkuId, batchId, MovementKind.Receipt, line.QuantityAccepted, StockReferenceKind.GoodsReceipt, receipt.Id, line.Id, receipt.Number, null));
        }

        var appended = await ledgerWriter.AppendAsync(drafts, CancellationToken.None);
        if (!appended.IsSuccess) return InventoryResponses.Fail(appended);

        await receipts.SaveAsync(receipt, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok(new PostedGoodsReceipt(receipt.Number, batchIds));
    }

    private sealed record Header(PurchaseOrderAggregate? Order, ConsignmentAggregate? Consignment, Guid? SupplierId, CommandResponse? Failure);

    /// <summary>The header rules that need other aggregates: the PO is Issued, of this branch and of the same supplier; the consignment is
    /// <c>Cleared</c> (<c>ConsignmentNotCleared</c>) and contains the PO when both are given.</summary>
    private async Task<Header> ResolveHeaderAsync(PostGoodsReceiptCommand command)
    {
        PurchaseOrderAggregate? order = null;
        if (command.PurchaseOrderId is { } orderId)
        {
            order = await orders.GetByIdAsync(orderId);
            if (order is null || order.BranchId != command.BranchId)
                return Refused(InventoryResponses.Fail(BusinessViolationCodes.PurchaseOrderNotFound, nameof(command.PurchaseOrderId), "That purchase order was not found at this branch."));
            if (order.Status != PurchaseOrderStatus.Issued)
                return Refused(InventoryResponses.Fail(BusinessViolationCodes.PurchaseOrderNotIssued, nameof(command.PurchaseOrderId), "Goods can only be received against an issued purchase order."));
            if (command.SupplierId is { } given && given != order.SupplierId)
                return Refused(InventoryResponses.Fail(BusinessViolationCodes.ReceiptSupplierMismatch, nameof(command.SupplierId), "The supplier is not the purchase order's supplier."));
        }

        var supplierId = order?.SupplierId ?? command.SupplierId;
        if (order is null && supplierId is { } supplier && await suppliers.GetByIdAsync(supplier) is null)
            return Refused(InventoryResponses.Fail(BusinessViolationCodes.SupplierNotFound, nameof(command.SupplierId), "That supplier was not found."));

        ConsignmentAggregate? consignment = null;
        if (command.ConsignmentId is { } consignmentId)
        {
            consignment = await consignments.GetByIdAsync(consignmentId);
            if (consignment is null || consignment.BranchId != command.BranchId)
                return Refused(InventoryResponses.Fail(BusinessViolationCodes.ConsignmentNotFound, nameof(command.ConsignmentId), "That consignment was not found at this branch."));
            if (consignment.Status != ConsignmentStatus.Cleared)
                return Refused(InventoryResponses.Fail(BusinessViolationCodes.ConsignmentNotCleared, nameof(command.ConsignmentId), "Goods can only be received against a cleared consignment."));
            if (order is not null && !consignment.PurchaseOrderIds.Contains(order.Id))
                return Refused(InventoryResponses.Fail(BusinessViolationCodes.ConsignmentPurchaseOrderMismatch, nameof(command.ConsignmentId), "The consignment does not contain that purchase order."));
        }

        return new Header(order, consignment, supplierId, null);

        static Header Refused(CommandResponse failure) => new(null, null, null, failure);
    }

    /// <summary>Per PO line: what was ordered and what earlier receipts accepted (ONE grouped read), at the order's unit cost and currency.</summary>
    private async Task<IReadOnlyDictionary<Guid, PurchaseOrderLineFact>> PurchaseOrderLineFactsAsync(PurchaseOrderAggregate? order)
    {
        if (order is null) return new Dictionary<Guid, PurchaseOrderLineFact>();

        var received = (await positions.GetPurchaseOrderReceiptsAsync(order.Id, CancellationToken.None)).ToDictionary(r => r.PurchaseOrderLineId);
        return order.Lines.ToDictionary(
            l => l.Id,
            l => new PurchaseOrderLineFact(l.Id, l.SkuId, l.OrderedQuantity, received.TryGetValue(l.Id, out var r) ? r.Accepted : 0m, l.UnitCostAmount, order.CurrencyCode));
    }
}
