using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Product;

/// <summary>
/// HO-SHP-001 section 8.11 -- <c>Id == CustomerId</c>, created on the first write (a first-write race
/// is a 409 through the primary key, section 12.5). Up to 200 items, unique per SKU. Adding a
/// present SKU is a no-op success (the methods return whether anything changed so the handler can
/// skip a pointless save).
/// </summary>
public sealed class WishlistAggregate : AggregateRoot
{
    private readonly List<WishlistItem> _items = [];

    public IReadOnlyList<WishlistItem> Items => _items;

    private WishlistAggregate() { }

    public WishlistAggregate(Guid customerId) : base(customerId) { }

    public bool AddItem(Guid skuId, Guid productId, DateTimeOffset now, UserContext userContext, Guid correlationId)
    {
        if (_items.Any(i => i.SkuId == skuId)) return false;

        if (_items.Count >= WishlistRules.MaxItems)
            throw new DomainRuleViolationException(BusinessViolationCodes.WishlistLimitReached,
                $"A wishlist can hold at most {WishlistRules.MaxItems} items.", nameof(skuId));

        _items.Add(new WishlistItem(skuId, productId, now));

        if (Version == 0) SetDefaultValue(userContext);

        AddDomainEvent(new WishlistItemAddedEvent { SkuId = skuId, ProductId = productId, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
        return true;
    }

    public bool RemoveItem(Guid skuId, UserContext userContext, Guid correlationId)
    {
        var item = _items.FirstOrDefault(i => i.SkuId == skuId);
        if (item is null) return false;

        _items.Remove(item);

        AddDomainEvent(new WishlistItemRemovedEvent { SkuId = skuId, ProductId = item.ProductId, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
        return true;
    }

    /// <summary>Anonymisation (ADR-096): the wishlist is emptied. Returns the number removed.</summary>
    public int Clear(UserContext userContext, Guid correlationId)
    {
        var removed = _items.Count;
        if (removed == 0) return 0;

        _items.Clear();
        UpdateVersion();
        return removed;
    }

    /// <summary>Merge (ADR-096): the duplicate's items are added to this (the survivor's) wishlist --
    /// a union, the 200-item cap respected. The newest duplicate items are kept first; the oldest
    /// beyond the cap are dropped and counted. Returns (moved, dropped).</summary>
    public (int Moved, int Dropped) MergeItemsFrom(IEnumerable<WishlistItem> duplicateItems, UserContext userContext)
    {
        var moved = 0;
        var dropped = 0;

        foreach (var item in duplicateItems.Where(i => _items.All(own => own.SkuId != i.SkuId)).OrderByDescending(i => i.AddedOn))
        {
            if (_items.Count >= WishlistRules.MaxItems)
            {
                dropped++;
                continue;
            }

            _items.Add(new WishlistItem(item.SkuId, item.ProductId, item.AddedOn));
            moved++;
        }

        if (moved > 0)
        {
            if (Version == 0) SetDefaultValue(userContext);
            UpdateVersion();
        }

        return (moved, dropped);
    }
}
