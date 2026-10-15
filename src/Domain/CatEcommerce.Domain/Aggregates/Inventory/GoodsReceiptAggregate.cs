using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Inventory;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>
/// HO-IMP-001 §8.6 (US-IMP-04). Posted in ONE command and immutable afterwards -- a mistake is corrected by an adjustment
/// (reason <c>Correction</c>). <see cref="Validate"/> is the one place every line rule lives (D-2 expiry, BR-3 expired goods,
/// dates, PO outstanding, cost and rate, location); the handler calls it to return EVERY violation at once, and
/// <see cref="Post"/> calls it again so the invariants hold whoever calls the aggregate (rule 20).
/// Header rules that need other aggregates (PO issued, supplier match, consignment cleared) are the handler's.
/// </summary>
public sealed class GoodsReceiptAggregate : AggregateRoot
{
    private readonly List<GoodsReceiptLine> _lines = [];

    public string Number { get; private set; } = default!;
    public Guid BranchId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public Guid? PurchaseOrderId { get; private set; }
    public Guid? ConsignmentId { get; private set; }
    public DateTimeOffset ReceivedOn { get; private set; }
    public Guid ReceivedBy { get; private set; }
    public string? DeliveryReference { get; private set; }
    public IReadOnlyList<GoodsReceiptLine> Lines => _lines;

    private GoodsReceiptAggregate() { }

    public GoodsReceiptAggregate(Guid id) : base(id) { }

    /// <summary>Every line violation, in line order, with property names like <c>Lines[2].ExpiresOn</c>.</summary>
    public static IReadOnlyList<GoodsReceiptViolation> Validate(GoodsReceiptPostDto dto, GoodsReceiptValidationContext context)
    {
        var violations = new List<GoodsReceiptViolation>();
        void Add(string code, string message, string property) => violations.Add(new GoodsReceiptViolation(code, message, property));

        if (dto.Lines.Count == 0 || dto.Lines.Count > InventoryRules.MaxReceiptLines)
        {
            Add(BusinessViolationCodes.GoodsReceiptLinesRequired, $"A goods receipt has between 1 and {InventoryRules.MaxReceiptLines} lines.", nameof(Lines));
            return violations;
        }

        if (dto.DeliveryReference is { Length: > 60 })
            Add(BusinessViolationCodes.GoodsReceiptLinesRequired, "A delivery reference cannot exceed 60 characters.", nameof(DeliveryReference));

        var acceptedPerPoLine = new Dictionary<Guid, decimal>();
        var seen = new HashSet<(Guid SkuId, string Lot, DateOnly? Expiry)>();

        for (var i = 0; i < dto.Lines.Count; i++)
        {
            var line = dto.Lines[i];
            var prefix = $"Lines[{i}]";

            if (!context.Skus.TryGetValue(line.SkuId, out var sku))
            {
                Add(BusinessViolationCodes.SkuNotFound, "That SKU does not exist.", $"{prefix}.SkuId");
                continue;
            }

            if (!sku.IsActive) Add(BusinessViolationCodes.SkuInactive, "That SKU is not active.", $"{prefix}.SkuId");

            // 1. Quantities.
            var quantitiesValid = true;
            foreach (var (value, property) in new[] { (line.QuantityAccepted, "QuantityAccepted"), (line.QuantityRejected, "QuantityRejected") })
            {
                if (value < 0m) { Add(BusinessViolationCodes.InvalidQuantity, "A quantity cannot be negative.", $"{prefix}.{property}"); quantitiesValid = false; }
                else if (value > 0m && !StockQuantity.TryValidate(value, sku.AllowsFractionalQuantity, out var code))
                {
                    Add(code!, code == BusinessViolationCodes.FractionalQuantityNotAllowed ? "This item can only be counted in whole units." : "A quantity has at most three decimal places.", $"{prefix}.{property}");
                    quantitiesValid = false;
                }
            }

            if (quantitiesValid && line.QuantityAccepted + line.QuantityRejected <= 0m)
                Add(BusinessViolationCodes.InvalidQuantity, "A line must receive or reject a quantity.", $"{prefix}.QuantityAccepted");
            if (line.QuantityRejected > 0m && line.RejectionReason is null)
                Add(BusinessViolationCodes.RejectionReasonRequired, "A rejected quantity needs a reason.", $"{prefix}.RejectionReason");
            if (line.RejectionNote is { Length: > 500 })
                Add(BusinessViolationCodes.RejectionReasonRequired, "A rejection note cannot exceed 500 characters.", $"{prefix}.RejectionNote");

            var accepted = line.QuantityAccepted > 0m;
            var lot = InventoryViolation.Normalise(line.LotNumber);

            // 2. Lot.
            if (accepted && (lot.Length == 0 || lot.Length > InventoryRules.MaxLotNumberLength))
                Add(BusinessViolationCodes.LotNumberRequired, "A lot number (at most 60 characters) is required for accepted stock.", $"{prefix}.LotNumber");

            // 3. D-2: food and medicine need an expiry.
            if (accepted && sku.ExpiryRequired && line.ExpiresOn is null)
                Add(BusinessViolationCodes.ExpiryDateRequired, InventoryMessages.ExpiryDateRequired, $"{prefix}.ExpiresOn");

            // 4. BR-3: expired goods are rejected, never accepted.
            if (accepted && BatchExpiryRules.IsExpired(line.ExpiresOn, context.BranchToday))
                Add(BusinessViolationCodes.BatchAlreadyExpired, "Expired goods cannot be accepted -- reject them with the reason Expired.", $"{prefix}.ExpiresOn");

            // 5. Dates.
            if (line.ManufacturedOn is { } manufactured && (manufactured > context.BranchToday || (line.ExpiresOn is { } expires && manufactured >= expires)))
                Add(BusinessViolationCodes.InvalidBatchDates, "The manufacture date must be in the past and before the expiry date.", $"{prefix}.ManufacturedOn");

            // 6. PO line.
            if (line.PurchaseOrderLineId is { } poLineId)
            {
                if (!context.PurchaseOrderLines.TryGetValue(poLineId, out var poLine) || poLine.SkuId != line.SkuId)
                    Add(BusinessViolationCodes.PurchaseOrderLineMismatch, "That purchase order line does not exist for this SKU.", $"{prefix}.PurchaseOrderLineId");
                else if (accepted)
                {
                    acceptedPerPoLine[poLineId] = acceptedPerPoLine.GetValueOrDefault(poLineId) + line.QuantityAccepted;
                    if (poLine.PreviouslyAccepted + acceptedPerPoLine[poLineId] > poLine.OrderedQuantity)
                        Add(BusinessViolationCodes.ReceiptExceedsOutstanding, $"Only {Math.Max(0m, poLine.OrderedQuantity - poLine.PreviouslyAccepted):0.###} is still outstanding on that order line.", $"{prefix}.QuantityAccepted");
                }
            }

            // 7./8. Cost and rate.
            if (accepted) ValidateCost(line, context, prefix, Add);

            // 9. Location.
            if (!context.Locations.TryGetValue(line.LocationId, out var location) || location.BranchId != dto.BranchId || !location.IsActive)
                Add(BusinessViolationCodes.StockLocationNotFound, "That location does not exist at this branch.", $"{prefix}.LocationId");
            else if (!StockLocationRules.Permits(location.Kind, MovementKind.Receipt))
                Add(BusinessViolationCodes.MovementNotPermittedAtLocation, "Stock cannot be received at that location.", $"{prefix}.LocationId");

            // 10. Duplicates (accepted lines only -- each makes a batch).
            if (accepted && !seen.Add((line.SkuId, lot, line.ExpiresOn)))
                Add(BusinessViolationCodes.DuplicateReceiptLine, InventoryMessages.DuplicateReceiptLine, $"{prefix}.LotNumber");
        }

        return violations;
    }

    /// <summary>The unit cost in the line's own currency and in base currency (rate applied at receipt). Null when the line has no
    /// usable cost -- <see cref="Validate"/> reports that.</summary>
    public static (decimal Amount, string Currency, decimal? Rate, decimal Base)? ResolveCost(GoodsReceiptLineDto line, GoodsReceiptValidationContext context)
    {
        decimal amount;
        string currency;
        if (line.PurchaseOrderLineId is { } poLineId && context.PurchaseOrderLines.TryGetValue(poLineId, out var poLine))
        {
            amount = poLine.UnitCostAmount;
            currency = poLine.CurrencyCode;
        }
        else if (line.UnitCostAmount is { } given && line.UnitCostCurrency is { Length: 3 } givenCurrency)
        {
            amount = given;
            currency = givenCurrency;
        }
        else return null;

        if (currency == context.BaseCurrency) return (amount, currency, null, decimal.Round(amount, UnitCost.Scale, MidpointRounding.ToEven));

        decimal rate;
        if (context.HasConsignment && context.ConsignmentRates.TryGetValue(currency, out var consignmentRate)) rate = consignmentRate;
        else if (line.ExchangeRateToBase is { } lineRate && !context.HasConsignment) rate = lineRate;
        else return null;

        return (amount, currency, rate, decimal.Round(amount * rate, UnitCost.Scale, MidpointRounding.ToEven));
    }

    public void Post(GoodsReceiptPostDto dto, GoodsReceiptValidationContext context)
    {
        var violations = Validate(dto, context);
        if (violations.Count > 0) throw InventoryViolation.Of(violations[0].Code, violations[0].Message, violations[0].PropertyName);

        Number = dto.Number;
        BranchId = dto.BranchId;
        SupplierId = dto.SupplierId;
        PurchaseOrderId = dto.PurchaseOrderId;
        ConsignmentId = dto.ConsignmentId;
        ReceivedOn = dto.ReceivedOn;
        ReceivedBy = dto.Actor.UserContext.UserId;
        DeliveryReference = string.IsNullOrWhiteSpace(dto.DeliveryReference) ? null : dto.DeliveryReference.Trim();

        _lines.Clear();
        foreach (var line in dto.Lines)
        {
            var sku = context.Skus[line.SkuId];
            var cost = line.QuantityAccepted > 0m ? ResolveCost(line, context) : null;
            _lines.Add(new GoodsReceiptLine(
                line.LineId, line.SkuId, sku.SkuCode, sku.DisplayName, sku.StockUnit, line.PurchaseOrderLineId,
                InventoryViolation.Normalise(line.LotNumber), line.ManufacturedOn, line.ExpiresOn, line.QuantityAccepted, line.QuantityRejected,
                line.QuantityRejected > 0m ? line.RejectionReason : null,
                string.IsNullOrWhiteSpace(line.RejectionNote) ? null : line.RejectionNote.Trim(),
                line.LocationId, line.HoldForInspection, cost?.Amount, cost?.Currency, cost?.Rate,
                line.QuantityAccepted > 0m ? line.BatchId ?? Guid.NewGuid() : null));
        }

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new GoodsReceiptPostedEvent
        {
            GoodsReceiptId = Id, BranchId = BranchId,
            BatchIds = _lines.Where(l => l.BatchId is not null).Select(l => l.BatchId!.Value).ToList(),
            SkuIds = _lines.Select(l => l.SkuId).Distinct().ToList(),
            CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext
        });
        UpdateVersion();
    }

    private static void ValidateCost(GoodsReceiptLineDto line, GoodsReceiptValidationContext context, string prefix, Action<string, string, string> add)
    {
        if (line.PurchaseOrderLineId is null)
        {
            if (line.UnitCostAmount is null || line.UnitCostCurrency is null)
            {
                add(BusinessViolationCodes.UnitCostRequired, "A unit cost is required when receiving without a purchase order.", $"{prefix}.UnitCostAmount");
                return;
            }

            if (line.UnitCostAmount < 0m || !InventoryRules.IsValidCurrencyCode(line.UnitCostCurrency))
            {
                add(BusinessViolationCodes.UnitCostRequired, "The unit cost must be zero or more, in a three-letter currency.", $"{prefix}.UnitCostAmount");
                return;
            }
        }

        if (ResolveCost(line, context) is null)
        {
            var currency = line.PurchaseOrderLineId is { } id && context.PurchaseOrderLines.TryGetValue(id, out var po) ? po.CurrencyCode : line.UnitCostCurrency;
            if (currency is not null && currency != context.BaseCurrency)
                add(BusinessViolationCodes.ExchangeRateMissing, $"No exchange rate to {context.BaseCurrency} is recorded for {currency}.", $"{prefix}.ExchangeRateToBase");
        }
        else if (line.ExchangeRateToBase is { } rate && rate <= 0m)
        {
            add(BusinessViolationCodes.InvalidExchangeRate, "An exchange rate must be greater than zero.", $"{prefix}.ExchangeRateToBase");
        }
    }
}
