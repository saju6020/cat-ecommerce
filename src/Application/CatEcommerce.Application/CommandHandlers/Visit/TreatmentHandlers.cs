using System.Security.Cryptography;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Visit;

using static VisitCommandHandlerSupport;

/// <summary>What the handlers need to say to Inventory about a visit -- one place, so a deduction and its reversal carry the same trace.</summary>
internal static class TreatmentStockTrace
{
    /// <summary>The ledger reference. <c>Visit</c> is HO-IMP-001's member for a visit (CONTRACTS D-2); the line id is the reference line, so one
    /// visit can have many movements and each is traced to its line (the recall trace reads reference + party, C-3). A request that draws from
    /// several batches is one ledger call whose reference line is the request's FIRST line id; each batch is also on its own line.</summary>
    public static StockReference Reference(Guid visitId, Guid lineId, string label) =>
        new(StockReferenceKind.Visit, visitId, lineId, $"Visit {visitId.ToString("N")[..12]} {label}");

    public static StockParty Party(CatAggregate? cat, Guid catId) => new(cat?.OwnerId, catId);
}

/// <summary>
/// A request records one treatment but may need several lines (one per batch drawn from). The client supplies the first line id (its idempotency key);
/// the others are derived from it deterministically, so a retry finds every line the first attempt made.
/// </summary>
internal static class TreatmentLineIds
{
    public static Guid For(Guid first, int index)
    {
        if (index == 0) return first;

        var input = first.ToByteArray().Concat(BitConverter.GetBytes(index)).ToArray();
        return new Guid(MD5.HashData(input));
    }
}

/// <summary>
/// C16 -- record a treatment (HO-EMR-001 section 8.6, US-CLN-15, ADR-126, ADR-127; owner decision C, 2026-10-13: auto-split across batches).
/// The handler orchestrates, the visit decides: everything outside the visit is read first (the SKU, the dispensary, the prices at now), then the
/// visit's one mutation method runs once per line, then Inventory's ledger rows are <b>enlisted</b> (never saved by the port), and <b>one</b> save
/// commits the visit and the movement together -- a rule-1 companion commit (F23, ADR-126). A stock refusal is a returned violation; nothing has been
/// written when it is returned.
///
/// <list type="number">
/// <item>A medicine or consumable names a SKU. A controlled drug is refused (<c>ControlledDrugNotSupportedHere</c>, ADR-126).</item>
/// <item><b>The draw.</b> Inventory's own FEFO plan over every batch the dispensary can issue decides the order; the request draws from the batches in
/// that order until the quantity is met, and <b>one line is created per batch used</b> (each line keeps its own lot, expiry and location).
/// A <b>named</b> batch is drawn first (it must not be expired: BR-3, refused before the override reason is even read; and when it is not the FEFO
/// first batch it needs a reason: BR-4). After the named batch the draw <b>continues in FEFO order over the remaining batches</b>, skipping the named one.</item>
/// <item><b>Available</b> is the whole dispensary's usable, unreserved stock of the SKU. Over it: without <c>AcceptShortfall</c> the answer is
/// <c>400 InsufficientStock</c> with that total (AC2 "I am warned"); with it, everything available is deducted across the batches and the remainder is the
/// shortfall -- recorded on the last line, and announced to Inventory through <c>RecordShortfall</c> (enlisted, not an event) for its reorder list.
/// With nothing available there is one batch-less line.</item>
/// <item><b>Idempotency.</b> The first line id is the client's: a retry (same id) finds the lines it already made and returns them, deducting nothing.</item>
/// <item>A race for the last units that the ledger sees when it plans is a <c>InsufficientStock</c> without detail (the controller answers <c>409</c>);
/// one it only sees at commit is the CHECK constraint, mapped to the same <c>409</c> by <c>UniqueConstraintExceptionFilter</c>.</item>
/// </list>
/// </summary>
public sealed class RecordTreatmentCommandHandler(
    IAggregateRootRepository<VisitAggregate> visitRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IStockLedger ledger,
    DispensaryBatchPicker batchPicker,
    IClinicalSkuReader skuReader,
    ISkuPriceReader skuPrices,
    IBillingPricingInputs servicePricing,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordTreatmentCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordTreatmentCommand command)
    {
        var visit = await visitRepository.GetByIdAsync(command.VisitId);
        if (visit is null || visit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.VisitNotFound, nameof(command.VisitId), ClinicalMessages.VisitNotFound);

        // Idempotent replay: the client's line id was already recorded on this visit (a retry after a timeout). Nothing is deducted again.
        var earlier = LinesOfRequest(visit, command.LineId);
        if (earlier.Count > 0) return Success(ResultOf(earlier, replayed: true));

        // Refuse a finalised visit before any read; the aggregate would refuse it too (EnsureOpen), but only after the stock has been planned.
        if (visit.Status != VisitStatus.Open)
            return Violation(BusinessViolationCodes.VisitFinalised, nameof(command.VisitId), ClinicalMessages.VisitFinalised);

        var kind = AppointmentMapper.ParseEnum<TreatmentKind>(command.Kind);
        var now = timeProvider.GetUtcNow();

        var resolved = kind.MovesStock
            ? await ResolveStockItemAsync(command, kind, visit, now)
            : await ResolveProcedureAsync(command, kind, now);
        if (resolved.Refusal is not null) return resolved.Refusal;
        var treatment = resolved.Treatment!;

        var lines = new List<TreatmentLine>();
        try
        {
            for (var i = 0; i < treatment.Draws.Count; i++)
                lines.Add(visit.RecordTreatment(treatment.ToDto(command, TreatmentLineIds.For(command.LineId, i), i, treatment.Draws[i], now)));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        // F23 / ADR-126 -- the companion commit. IStockLedger validates and ENLISTS the ledger rows in the shared unit of work without saving; the
        // visit's single UpdateAsync below commits the lines and the movement (and the shortfall alert) together, or neither.
        var drawn = treatment.Draws.Where(d => d.Deducted > 0m).ToList();
        if (drawn.Count > 0)
        {
            var cat = await catRepository.GetByIdAsync(visit.CatId);
            var deduction = await ledger.DeductAsync(
                new StockDemand(
                    visit.BranchId, treatment.SkuId!.Value, drawn.Sum(d => d.Deducted), MovementKind.ClinicalUse, null,
                    drawn.Select(d => (d.BatchId!.Value, d.LocationId!.Value, d.Deducted)).ToList(), treatment.OverrideReason),
                TreatmentStockTrace.Reference(visit.Id, command.LineId, "treatment"), TreatmentStockTrace.Party(cat, visit.CatId), CancellationToken.None);

            if (!deduction.IsSuccess) return LedgerRefusal(deduction.ViolationCode!, deduction.Message, deduction.PropertyName);
        }

        var shortfall = lines.Sum(l => l.ShortfallQuantity);
        if (shortfall > 0m)
            ledger.RecordShortfall(new StockShortfall(treatment.SkuId!.Value, treatment.DispensaryLocationId, shortfall, visit.Id, lines[^1].Id));

        await visitRepository.UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly);

        return Success(ResultOf(lines, replayed: false));
    }

    // ── the request's lines and the result ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The lines an earlier attempt of this request made: the client's id, then each derived id while it exists.</summary>
    private static List<TreatmentLine> LinesOfRequest(VisitAggregate visit, Guid firstLineId)
    {
        var lines = new List<TreatmentLine>();
        for (var i = 0; i < ClinicalNoteRules.MaxTreatmentLines; i++)
        {
            var id = TreatmentLineIds.For(firstLineId, i);
            var line = visit.Treatments.FirstOrDefault(t => t.Id == id);
            if (line is null) break;
            lines.Add(line);
        }

        return lines;
    }

    private static RecordTreatmentResult ResultOf(IReadOnlyList<TreatmentLine> lines, bool replayed)
    {
        var results = lines.Select(l => new TreatmentLineResult(
            l.Id, l.Quantity, l.DeductedQuantity, l.ShortfallQuantity,
            l.BatchId is { } batchId && l.DeductedQuantity > 0m ? new TreatmentBatchResult(batchId, l.LotNumber!, l.BatchExpiresOn) : null)).ToList();

        return new RecordTreatmentResult(
            lines[0].Id, lines.Sum(l => l.DeductedQuantity), lines.Sum(l => l.ShortfallQuantity), lines[0].StockUnit ?? "each",
            results.Select(r => r.Batch).FirstOrDefault(b => b is not null), results, replayed);
    }

    // ── a procedure: no stock, a service price when billable ───────────────────────────────────────────────────────────────────────

    private async Task<Resolution> ResolveProcedureAsync(RecordTreatmentCommand command, TreatmentKind kind, DateTimeOffset now)
    {
        Money? price = null;
        Guid? taxClassId = null;

        if (command.ServiceId is { } serviceId)
        {
            var service = await servicePricing.GetServicePriceAsync(serviceId, now);
            if (service is null)
                return Resolution.Refused(Violation(BusinessViolationCodes.ServiceNotFound, nameof(command.ServiceId), "That service does not exist."));

            // ADR-127: the service's price and tax class at the moment of recording, snapshotted -- never re-read.
            if (command.IsBillable)
            {
                if (service.Price is null)
                    return Resolution.Refused(Violation(BusinessViolationCodes.ServiceNotPriced, nameof(command.ServiceId), "That service has no price yet, so it cannot be billed."));
                price = service.Price;
                taxClassId = service.TaxClassId;
            }
        }
        else if (command.IsBillable)
        {
            return Resolution.Refused(Violation(
                BusinessViolationCodes.ServiceNotPriced, nameof(command.ServiceId), "Choose a priced service to bill this procedure, or record it as not billable."));
        }

        return Resolution.Of(new ResolvedTreatment(
            kind, (command.Description ?? string.Empty).Trim(), command.ServiceId, null, null, null, price, taxClassId, null, Guid.Empty,
            [new Draw(command.Quantity, null, null, null, null, 0m, 0m, null)]));
    }

    // ── a medicine or consumable: the SKU, the draw across batches, the quantity check, the price ───────────────────────────────

    private async Task<Resolution> ResolveStockItemAsync(RecordTreatmentCommand command, TreatmentKind kind, VisitAggregate visit, DateTimeOffset now)
    {
        var sku = command.SkuId is { } skuId ? await skuReader.GetAsync(skuId, CancellationToken.None) : null;
        if (sku is null)
            return Resolution.Refused(Violation(BusinessViolationCodes.SkuNotFound, nameof(command.SkuId), "That item does not exist."));
        if (!sku.IsActive)
            return Resolution.Refused(Violation(BusinessViolationCodes.SkuInactive, nameof(command.SkuId), "That item is no longer in use. Choose another."));
        if (sku.IsControlledDrug)
            return Resolution.Refused(Violation(BusinessViolationCodes.ControlledDrugNotSupportedHere, nameof(command.SkuId), ClinicalMessages.ControlledDrugNotSupportedHere));

        // Whole units unless the SKU allows fractions (HO-IMP-001 OQ-19: none does today) -- refused here so a request is never "recorded" at a
        // quantity the ledger would refuse at the deduction.
        if (!StockQuantity.TryValidate(command.Quantity, sku.AllowsFractionalQuantity, out var quantityCode))
        {
            return Resolution.Refused(Violation(
                quantityCode!, nameof(command.Quantity),
                quantityCode == BusinessViolationCodes.FractionalQuantityNotAllowed
                    ? $"{sku.Name} can only be counted in whole {sku.StockUnit}."
                    : "The quantity must be greater than zero, with at most three decimal places."));
        }

        var stock = await batchPicker.ReadAsync(visit.BranchId, sku.SkuId, CancellationToken.None);
        if (!stock.IsSuccess)
        {
            return Resolution.Refused(stock.ViolationCode == "DispensaryNotFound"
                ? Violation(BusinessViolationCodes.DispensaryNotConfigured, "BranchId", ClinicalMessages.DispensaryNotConfigured)
                : Violation(stock.ViolationCode!, stock.PropertyName ?? "SkuId", stock.Message ?? "The clinic stock could not be read."));
        }

        var batches = stock.Value!.Batches;
        var fefoFirst = batches.FirstOrDefault(b => b.IsFefoDefault);
        DispensaryBatchChoice? pinned = null;
        string? overrideReason = null;

        if (command.BatchId is { } batchId)
        {
            pinned = batches.FirstOrDefault(b => b.BatchId == batchId);
            if (pinned is null)
                return Resolution.Refused(Violation(BusinessViolationCodes.BatchNotIssuable, nameof(command.BatchId), ClinicalMessages.BatchNotIssuable));

            // BR-3 / FR-IMP-9 first: an expired batch is refused whatever the reason, so the override reason is not even looked at.
            if (pinned.RefusalCode is { } refusal)
            {
                return Resolution.Refused(Violation(
                    refusal == BusinessViolationCodes.BatchExpired ? BusinessViolationCodes.BatchExpired : BusinessViolationCodes.BatchNotIssuable,
                    nameof(command.BatchId), pinned.RefusalMessage ?? ClinicalMessages.BatchNotIssuable));
            }

            // BR-4: not the batch that expires first needs a reason.
            if (fefoFirst is not null && fefoFirst.BatchId != pinned.BatchId)
            {
                overrideReason = command.BatchOverrideReason?.Trim();
                if (overrideReason is null || overrideReason.Length < ClinicalNoteRules.MinBatchOverrideReasonLength)
                    return Resolution.Refused(Violation(BusinessViolationCodes.BatchOverrideReasonRequired, nameof(command.BatchOverrideReason), ClinicalMessages.BatchOverrideReasonRequired));
            }
        }

        // The whole dispensary's usable stock of this SKU, in Inventory's own FEFO order (its plan, so the order and the free quantities are exactly the ledger's).
        var available = batches.Where(b => b.IsIssuable).Sum(b => b.Available);
        var fefoLines = new List<StockMovementLine>();
        if (available > 0m)
        {
            var plan = await ledger.PlanAsync(new StockDemand(visit.BranchId, sku.SkuId, available, MovementKind.ClinicalUse, null, null, null), CancellationToken.None);
            if (!plan.IsSuccess) return Resolution.Refused(LedgerRefusal(plan.ViolationCode!, plan.Message, plan.PropertyName));
            fefoLines.AddRange(plan.Value!.Lines);
        }

        decimal deducted;
        decimal shortfall;
        if (available >= command.Quantity)
        {
            deducted = command.Quantity;
            shortfall = 0m;
        }
        else if (!command.AcceptShortfall)
        {
            // AC2 "I am warned": nothing is written; the client may re-post with AcceptShortfall. The figure is the whole dispensary's, not one batch's.
            var warning = Violation(
                BusinessViolationCodes.InsufficientStock, nameof(command.Quantity), ClinicalMessages.InsufficientStockInDispensary(available, sku.StockUnit, sku.Name));
            warning.Result = new InsufficientStockDetail(available, sku.StockUnit);
            return Resolution.Refused(warning);
        }
        else
        {
            deducted = available;
            shortfall = command.Quantity - available;
        }

        // The draw: the named batch first (if any), then FEFO order skipping it, until `deducted` is met.
        var order = new List<StockMovementLine>();
        if (pinned is not null && pinned.Available > 0m)
            order.Add(fefoLines.FirstOrDefault(l => l.BatchId == pinned.BatchId) ?? new StockMovementLine(pinned.BatchId, pinned.LotNumber, pinned.ExpiresOn, pinned.LocationId, pinned.Available, false));
        order.AddRange(fefoLines.Where(l => pinned is null || l.BatchId != pinned.BatchId));

        var draws = new List<Draw>();
        var remaining = deducted;
        foreach (var line in order)
        {
            if (remaining <= 0m) break;
            var take = Math.Min(remaining, line.Quantity);
            draws.Add(new Draw(take, line.BatchId, line.LotNumber, line.ExpiresOn, line.LocationId, take, 0m, pinned is not null && line.BatchId == pinned.BatchId ? overrideReason : null));
            remaining -= take;
        }

        // The shortfall rides on the last line (its quantity is what was given there plus what the shelf could not supply); with nothing drawn it is one batch-less line.
        if (shortfall > 0m)
        {
            if (draws.Count == 0) draws.Add(new Draw(shortfall, null, null, null, null, 0m, shortfall, null));
            else draws[^1] = draws[^1] with { Quantity = draws[^1].Quantity + shortfall, Shortfall = shortfall };
        }

        // ADR-127: the SKU's price and tax class at the moment of recording, snapshotted.
        Money? price = null;
        Guid? taxClassId = null;
        if (command.IsBillable)
        {
            price = await skuPrices.PriceAtAsync(sku.SkuId, now, CancellationToken.None);
            if (price is null)
                return Resolution.Refused(Violation(BusinessViolationCodes.SkuNotPriced, nameof(command.IsBillable), ClinicalMessages.SkuNotPriced));
            taxClassId = sku.TaxClassId;
        }

        return Resolution.Of(new ResolvedTreatment(
            kind, string.IsNullOrWhiteSpace(command.Description) ? sku.Name : command.Description.Trim(), null, sku.SkuId, sku.Name, sku.StockUnit,
            price, taxClassId, draws.Any(d => d.OverrideReason is not null) ? overrideReason : null, stock.Value!.Location.LocationId, draws));
    }

    /// <summary>A refusal from Inventory's port, in the clinic's own words where the handover names them. <c>InsufficientStock</c> here means the
    /// last units went between the read and the deduction: no detail, and the controller answers <c>409</c> (IMP-5).</summary>
    private static CommandResponse LedgerRefusal(string code, string? message, string? property) => code switch
    {
        _ when code == BusinessViolationCodes.BatchNotUsable => Violation(BusinessViolationCodes.BatchNotIssuable, "BatchId", ClinicalMessages.BatchNotIssuable),
        _ when code == BusinessViolationCodes.AllocationOverrideReasonRequired => Violation(BusinessViolationCodes.BatchOverrideReasonRequired, "BatchOverrideReason", ClinicalMessages.BatchOverrideReasonRequired),
        _ => Violation(code, property ?? "Quantity", message ?? "The stock could not be taken."),
    };

    // ── plumbing ────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>One line to record: what was given from one batch (or, batch-less, only a shortfall).</summary>
    private sealed record Draw(decimal Quantity, Guid? BatchId, string? LotNumber, DateOnly? ExpiresOn, Guid? LocationId, decimal Deducted, decimal Shortfall, string? OverrideReason);

    private sealed record ResolvedTreatment(
        TreatmentKind Kind, string Description, Guid? ServiceId, Guid? SkuId, string? SkuName, string? StockUnit,
        Money? Price, Guid? TaxClassId, string? OverrideReason, Guid DispensaryLocationId, IReadOnlyList<Draw> Draws)
    {
        public TreatmentDto ToDto(RecordTreatmentCommand command, Guid lineId, int position, Draw draw, DateTimeOffset now) => new(
            lineId, Kind, Description, ServiceId, SkuId, SkuName, StockUnit, draw.BatchId, draw.LotNumber, draw.ExpiresOn, draw.LocationId,
            draw.Quantity, Kind.MovesStock ? draw.Deducted : 0m, Kind.MovesStock ? draw.Shortfall : 0m,
            command.Dose, command.Route, command.IsBillable, Price, TaxClassId, draw.OverrideReason,
            command.UserContext.UserId, now, command.UserContext, command.CorrelationId, position);
    }

    private sealed record Resolution(ResolvedTreatment? Treatment, CommandResponse? Refusal)
    {
        public static Resolution Of(ResolvedTreatment treatment) => new(treatment, null);

        public static Resolution Refused(CommandResponse refusal) => new(null, refusal);
    }
}

/// <summary>
/// C17 -- reverse a treatment line (HO-EMR-001 section 8.6, US-CLN-15, ADR-126). The visit marks the line <c>Reversed</c> (it must be open, the
/// line must exist and not already be reversed); when stock was deducted, the compensating movement is <b>enlisted</b>
/// (<c>IStockLedger.ReceiveReturnAsync</c>) and the same save commits both (F23). Inventory writes the units into the branch's Quarantine
/// location, not back to the dispensary (HO-IMP-001 section 8.8 rule 8, OQ-19): an auditable round trip, and an expired batch can never be
/// returned to sellable stock this way.
/// </summary>
public sealed class ReverseTreatmentLineCommandHandler(
    IAggregateRootRepository<VisitAggregate> visitRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IStockLedger ledger,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ReverseTreatmentLineCommand>
{
    /// <summary>What Inventory records beside the returned stock. The vet's own reason is clinical free text and stays on the visit; it is
    /// never copied into a stock table.</summary>
    private const string ReturnNote = "Treatment reversed on the visit";

    public async Task<CommandResponse> HandleAsync(ReverseTreatmentLineCommand command)
    {
        var visit = await visitRepository.GetByIdAsync(command.VisitId);
        if (visit is null || visit.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.VisitNotFound, nameof(command.VisitId), ClinicalMessages.VisitNotFound);

        TreatmentLine line;
        try
        {
            line = visit.ReverseTreatment(new TreatmentReversalDto(
                command.LineId, command.Reason, command.UserContext.UserId, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        // F23 / ADR-126 -- the compensating movement is enlisted, never saved by the port; the single UpdateAsync commits it with the reversal.
        if (line.DeductedQuantity > 0m && line.BatchId is { } batchId && line.SkuId is { } skuId)
        {
            var cat = await catRepository.GetByIdAsync(visit.CatId);
            var returned = await ledger.ReceiveReturnAsync(
                new StockReturn(visit.BranchId, skuId, batchId, line.DeductedQuantity, ReturnNote),
                TreatmentStockTrace.Reference(visit.Id, line.Id, "reversal"), TreatmentStockTrace.Party(cat, visit.CatId), CancellationToken.None);

            if (!returned.IsSuccess)
                return Violation(returned.ViolationCode!, returned.PropertyName ?? "LineId", returned.Message ?? "The stock could not be returned.");
        }

        await visitRepository.UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
