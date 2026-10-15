namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-ORD-001 §10.3 / ADR-199 -- the guest cart cookie and the cart bounds.</summary>
public static class CartRules
{
    /// <summary>The opaque guest-cart cookie (HttpOnly; Secure; SameSite=Lax). Only the SHA-256 of its 32-byte token is stored.</summary>
    public const string GuestCookieName = "pc_cart";

    public const int TokenBytes = 32;
    public const int MaxLinesHardCap = 50;
    public const int PurgeBatchSize = 200;
}
