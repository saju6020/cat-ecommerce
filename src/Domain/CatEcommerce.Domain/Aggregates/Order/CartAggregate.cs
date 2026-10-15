using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Order;

/// <summary>What a merge did: how many lines the account cart gained or changed, and the guest lines it had no room for.</summary>
public sealed record CartMergeResult(int MergedLineCount, IReadOnlyList<Guid> DroppedSkuIds);

/// <summary>
/// HO-ORD-001 §8.2 (ADR-199), US-ORD-01 -- a server-side cart. A <b>guest</b> cart is found by the SHA-256 of an opaque 32-byte cookie token; a
/// <b>customer</b> cart is the customer's one <c>Active</c> cart (unique among active carts, so the cart follows the customer across devices).
/// Merging a guest cart into the account cart is an explicit, idempotent act that takes the <b>larger</b> quantity per <c>(SKU, cat)</c> key
/// (OQ-3), never the sum, and marks the guest cart <c>Merged</c>. A cart reserves nothing and carries no price of its own. Carts raise no domain
/// events: nothing subscribes, and the audit interceptor records the changes (OQ-18).
///
/// The aggregate enforces what it can see of itself: quantity bounds, the line cap, a line key at most once. "The SKU is purchasable", "the cat is
/// the caller's and authorised" and "stock exists" span other records and are the handler's checks.
/// </summary>
public sealed class CartAggregate : AggregateRoot
{
    private readonly List<CartLine> _lines = [];

    public CartOwnerKind OwnerKind { get; private set; } = default!;
    public Guid? CustomerId { get; private set; }
    public string? GuestTokenHash { get; private set; }
    public CartStatus Status { get; private set; } = default!;
    public Guid? MergedIntoCartId { get; private set; }
    public DateTimeOffset LastActivityOn { get; private set; }

    public IReadOnlyList<CartLine> Lines => _lines;

    private CartAggregate() { }

    public CartAggregate(Guid id) : base(id) { }

    public void OpenForGuest(string tokenHash, DateTimeOffset now, Platform.Infrastructure.Common.Security.UserContext userContext)
    {
        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length != 64)
            throw new ArgumentException("A guest cart is keyed by the 64-character SHA-256 of its token.", nameof(tokenHash));

        OwnerKind = CartOwnerKind.Guest;
        GuestTokenHash = tokenHash;
        Status = CartStatus.Active;
        LastActivityOn = now;
        SetDefaultValue(userContext);
        UpdateVersion();
    }

    public void OpenForCustomer(Guid customerId, DateTimeOffset now, Platform.Infrastructure.Common.Security.UserContext userContext)
    {
        OwnerKind = CartOwnerKind.Customer;
        CustomerId = customerId;
        Status = CartStatus.Active;
        LastActivityOn = now;
        SetDefaultValue(userContext);
        UpdateVersion();
    }

    /// <summary>Adds <paramref name="quantity"/> of a SKU (for a cat, when the SKU needs one). An existing key becomes <c>min(existing + quantity,
    /// max)</c> and keeps its price-when-added; a new key is a new line priced at the SKU's current price (<c>CartFull</c> at the cap).</summary>
    public Guid AddLine(Guid skuId, int quantity, Guid? catId, decimal currentPrice, string currencyCode, DateTimeOffset now, int maxQuantityPerLine, int maxLines)
    {
        EnsureActive();
        RequireQuantity(quantity, maxQuantityPerLine);

        var existing = _lines.FirstOrDefault(l => l.HasKey(skuId, catId));
        if (existing is not null)
        {
            existing.SetQuantity(Math.Min(existing.Quantity + quantity, maxQuantityPerLine), now);
            Touch(now);
            return existing.Id;
        }

        if (_lines.Count >= Math.Min(maxLines, CartRules.MaxLinesHardCap))
            throw new DomainRuleViolationException(BusinessViolationCodes.CartFull, OrderMessages.CartFull, nameof(Lines));

        var line = new CartLine(Guid.NewGuid(), skuId, catId, quantity, currentPrice, currencyCode, now);
        _lines.Add(line);
        Touch(now);
        return line.Id;
    }

    /// <summary>Sets a line's quantity (1 to the maximum -- 0 is not accepted; remove the line instead).</summary>
    public void SetQuantity(Guid lineId, int quantity, DateTimeOffset now, int maxQuantityPerLine)
    {
        EnsureActive();
        RequireQuantity(quantity, maxQuantityPerLine);
        Line(lineId).SetQuantity(quantity, now);
        Touch(now);
    }

    /// <summary>Re-points a line at another cat. The new key must not collide with another line (<c>DuplicateCartLine</c>).</summary>
    public void SetLineCat(Guid lineId, Guid? catId, DateTimeOffset now)
    {
        EnsureActive();
        var line = Line(lineId);

        if (_lines.Any(l => l.Id != lineId && l.HasKey(line.SkuId, catId)))
            throw new DomainRuleViolationException(BusinessViolationCodes.DuplicateCartLine, OrderMessages.DuplicateCartLine, nameof(CartLine.CatId));

        line.SetCat(catId, now);
        Touch(now);
    }

    /// <summary>Removes a line; removing one that is not there is a no-op (idempotent).</summary>
    public bool RemoveLine(Guid lineId, DateTimeOffset now)
    {
        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null) return false;

        _lines.Remove(line);
        Touch(now);
        return true;
    }

    /// <summary>Removes the lines an order just took. Lines already gone are ignored.</summary>
    public void RemovePlacedLines(IEnumerable<Guid> lineIds, DateTimeOffset now)
    {
        var ids = lineIds.ToHashSet();
        if (_lines.RemoveAll(l => ids.Contains(l.Id)) > 0) Touch(now);
    }

    /// <summary>
    /// Absorbs a guest cart (ADR-199, OQ-3). For each guest line: the key exists here -> the quantity becomes the <b>larger</b> of the two, the older
    /// line's price-when-added kept; the key does not exist -> the line is copied (beyond the cap it is dropped and reported). The guest cart becomes
    /// <c>Merged</c>. Merging a guest cart that is not active, or the same cart, changes nothing.
    /// </summary>
    public CartMergeResult MergeFrom(CartAggregate guest, DateTimeOffset now, int maxQuantityPerLine, int maxLines) =>
        guest.OwnerKind != CartOwnerKind.Guest ? new CartMergeResult(0, []) : Absorb(guest, now, maxQuantityPerLine, maxLines);

    /// <summary>A merged-away customer's active cart is absorbed into the survivor's the same way (HO-ACC-001 ADR-039): the larger quantity per key, no duplicate
    /// line, the duplicate's cart <c>Merged</c>.</summary>
    public CartMergeResult AbsorbDuplicateCustomerCart(CartAggregate duplicate, DateTimeOffset now, int maxQuantityPerLine, int maxLines) =>
        duplicate.OwnerKind != CartOwnerKind.Customer ? new CartMergeResult(0, []) : Absorb(duplicate, now, maxQuantityPerLine, maxLines);

    private CartMergeResult Absorb(CartAggregate guest, DateTimeOffset now, int maxQuantityPerLine, int maxLines)
    {
        EnsureActive();
        if (guest.Id == Id || guest.Status != CartStatus.Active)
            return new CartMergeResult(0, []);

        var changed = 0;
        var dropped = new List<Guid>();
        var cap = Math.Min(maxLines, CartRules.MaxLinesHardCap);

        foreach (var incoming in guest._lines)
        {
            var existing = _lines.FirstOrDefault(l => l.HasKey(incoming.SkuId, incoming.CatId));
            if (existing is not null)
            {
                var larger = Math.Min(Math.Max(existing.Quantity, incoming.Quantity), maxQuantityPerLine);
                if (larger != existing.Quantity) existing.SetQuantity(larger, now);
                existing.KeepOlderPrice(incoming);
                changed++;
                continue;
            }

            if (_lines.Count >= cap)
            {
                dropped.Add(incoming.SkuId);
                continue;
            }

            _lines.Add(new CartLine(Guid.NewGuid(), incoming.SkuId, incoming.CatId, Math.Min(incoming.Quantity, maxQuantityPerLine), incoming.PriceWhenAdded, incoming.CurrencyCode, incoming.AddedOn));
            changed++;
        }

        guest.MarkMerged(Id, now);
        Touch(now);
        return new CartMergeResult(changed, dropped);
    }

    /// <summary>A guest cart past retention is abandoned. Only a guest cart can be.</summary>
    public void Abandon(DateTimeOffset now)
    {
        if (OwnerKind != CartOwnerKind.Guest)
            throw new InvalidOperationException("Only a guest cart can be abandoned.");

        if (Status != CartStatus.Active) return;

        Status = CartStatus.Abandoned;
        LastActivityOn = now;
        UpdateVersion();
    }

    /// <summary>Re-keys a customer cart to the surviving customer (HO-ACC-001 ADR-039). Idempotent.</summary>
    public bool ReassignCustomer(Guid survivingCustomerId)
    {
        if (CustomerId == survivingCustomerId) return false;

        CustomerId = survivingCustomerId;
        UpdateVersion();
        return true;
    }

    /// <summary>Anonymisation of the customer (HO-ACC-001): an active customer cart is abandoned so nothing is bought later on the account.</summary>
    public bool AbandonForAnonymisation(DateTimeOffset now)
    {
        if (Status != CartStatus.Active) return false;

        Status = CartStatus.Abandoned;
        _lines.Clear();
        LastActivityOn = now;
        UpdateVersion();
        return true;
    }

    private void MarkMerged(Guid intoCartId, DateTimeOffset now)
    {
        Status = CartStatus.Merged;
        MergedIntoCartId = intoCartId;
        LastActivityOn = now;
        UpdateVersion();
    }

    private void Touch(DateTimeOffset now)
    {
        LastActivityOn = now;
        UpdateVersion();
    }

    private CartLine Line(Guid lineId) =>
        _lines.FirstOrDefault(l => l.Id == lineId)
        ?? throw new DomainRuleViolationException(BusinessViolationCodes.CartLineNotFound, "That cart line was not found.", nameof(lineId));

    private void EnsureActive()
    {
        if (Status != CartStatus.Active)
            throw new DomainRuleViolationException(BusinessViolationCodes.CartNotFound, "This cart is no longer active.", nameof(Status));
    }

    private static void RequireQuantity(int quantity, int maxQuantityPerLine)
    {
        if (quantity < 1 || quantity > maxQuantityPerLine)
            throw new DomainRuleViolationException(BusinessViolationCodes.QuantityOutOfRange, $"Choose a quantity from 1 to {maxQuantityPerLine}.", nameof(CartLine.Quantity));
    }
}
