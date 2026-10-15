using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Order;

/// <summary>
/// HO-ORD-001 §11.1 C5 (US-ORD-01, US-SHP-10 AC2) -- add a SKU to the caller's cart. The caller is the customer in <c>UserContext</c>, or a guest
/// identified by <see cref="GuestTokenHash"/> (the SHA-256 of the <c>pc_cart</c> cookie the controller read). For a guest with no live cart the
/// controller also supplies <see cref="NewGuestTokenHash"/>, the hash of a token it minted; the handler uses it only if it has to create the cart
/// (<see cref="AddCartLineResult.GuestCartCreated"/>), and the controller then sets that token as the cookie.
/// </summary>
public sealed class AddCartLineCommand : Command
{
    public required Guid NewCartId { get; init; }
    public string? GuestTokenHash { get; init; }
    public string? NewGuestTokenHash { get; init; }
    public required Guid SkuId { get; init; }
    public required int Quantity { get; init; }
    public Guid? CatId { get; init; }
}

/// <summary>The result of C5.</summary>
public sealed record AddCartLineResult(Guid LineId, Guid CartId, bool GuestCartCreated);

/// <summary>C6 -- change a line's quantity and/or its cat. The cart is the caller's own: another caller's line is not found.</summary>
public sealed class UpdateCartLineCommand : Command
{
    public required Guid LineId { get; init; }
    public string? GuestTokenHash { get; init; }
    public int? Quantity { get; init; }
    public Guid? CatId { get; init; }
}

/// <summary>C6 -- remove a line (idempotent).</summary>
public sealed class RemoveCartLineCommand : Command
{
    public required Guid LineId { get; init; }
    public string? GuestTokenHash { get; init; }
}

/// <summary>C7 (ADR-199) -- merge the guest cart named by the cookie hash into the signed-in customer's cart. Idempotent: a missing or already
/// merged guest cart succeeds and writes nothing.</summary>
public sealed class MergeGuestCartCommand : Command
{
    public required Guid NewCartId { get; init; }
    public required string GuestTokenHash { get; init; }
}

/// <summary>The result of C7: how many lines the customer cart gained or changed, and the guest lines it had no room for.</summary>
public sealed record MergeGuestCartResult(int MergedLineCount, IReadOnlyList<DroppedCartLine> DroppedLines);

public sealed record DroppedCartLine(string ProductName, string Reason);

/// <summary>C8 -- abandon one batch (at most 200) of guest carts idle past <c>orders.guestCartRetentionDays</c>. Sent by the worker only.</summary>
public sealed class PurgeStaleGuestCartsCommand : Command;

public sealed record PurgeStaleGuestCartsResult(int Abandoned);
