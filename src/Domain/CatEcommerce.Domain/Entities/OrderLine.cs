using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-ORD-001 §8.3.1 -- one line of a placed order, a full snapshot of what was sold and at what price, tax and discount (HO-ADM-001 ADR-049): later
/// price, rate or catalogue changes never touch it. <b>Every line records the batches it was allocated</b> (<see cref="Allocations"/>, D-4), so a recall
/// traces batch -> order -> customer -> cat. <c>QuantityReturned</c> counts units received back through the returns path.
/// </summary>
public sealed class OrderLine
{
    private readonly List<OrderLineAllocation> _allocations = [];

    public Guid Id { get; private set; }
    public int LineNumber { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public string VariantLabel { get; private set; } = default!;
    public string? Barcode { get; private set; }
    public ProductReturnClass ReturnClass { get; private set; } = default!;
    public bool IsBundle { get; private set; }
    public Guid? CatId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineSubtotal { get; private set; }
    public decimal LineDiscount { get; private set; }
    public decimal OrderDiscountShare { get; private set; }
    public Guid TaxClassId { get; private set; }
    public decimal TaxRatePercent { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal LineTotal { get; private set; }
    public int QuantityReturned { get; private set; }

    /// <summary>The components a bundle line is made of (SKU and quantity per bundle), kept so allocation can be checked per component.</summary>
    public IReadOnlyList<OrderLineComponent> Components { get; private set; } = [];

    public IReadOnlyList<OrderLineAllocation> Allocations => _allocations;

    private OrderLine() { }

    public OrderLine(
        Guid id, int lineNumber, Guid skuId, Guid productId, string productName, string variantLabel, string? barcode, ProductReturnClass returnClass, bool isBundle,
        Guid? catId, int quantity, decimal unitPrice, decimal lineSubtotal, decimal lineDiscount, decimal orderDiscountShare, Guid taxClassId, decimal taxRatePercent,
        decimal taxAmount, decimal lineTotal, IReadOnlyList<OrderLineComponent> components)
    {
        Id = id;
        LineNumber = lineNumber;
        SkuId = skuId;
        ProductId = productId;
        ProductName = productName;
        VariantLabel = variantLabel;
        Barcode = barcode;
        ReturnClass = returnClass;
        IsBundle = isBundle;
        CatId = catId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineSubtotal = lineSubtotal;
        LineDiscount = lineDiscount;
        OrderDiscountShare = orderDiscountShare;
        TaxClassId = taxClassId;
        TaxRatePercent = taxRatePercent;
        TaxAmount = taxAmount;
        LineTotal = lineTotal;
        Components = components;
    }

    /// <summary>What must be allocated for this line, per SKU: the line's own SKU, or each component times the quantity for a bundle.</summary>
    public IReadOnlyDictionary<Guid, int> RequiredBySku() =>
        IsBundle
            ? Components.GroupBy(c => c.SkuId).ToDictionary(g => g.Key, g => g.Sum(c => c.Quantity) * Quantity)
            : new Dictionary<Guid, int> { [SkuId] = Quantity };

    internal void ReplaceAllocations(IEnumerable<OrderLineAllocation> allocations)
    {
        _allocations.Clear();
        _allocations.AddRange(allocations);
    }

    internal void AddReturned(int quantity) => QuantityReturned += quantity;

}

/// <summary>One component of a bundle line: the component SKU and how many make one bundle.</summary>
public sealed record OrderLineComponent(Guid SkuId, int Quantity);
