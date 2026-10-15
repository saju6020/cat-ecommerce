using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Api.Ordering;

/// <summary>
/// HO-ORD-001 §12.4 / ADR-199 -- reads and writes the opaque guest-cart cookie <c>pc_cart</c>: <c>HttpOnly; Secure; SameSite=Lax</c>, path <c>/</c>,
/// lifetime the guest-cart retention. The cookie value is a bearer token (32 random bytes, base64url) and is never logged or returned in a body; only
/// its SHA-256 reaches the database. A cookie that is missing, malformed or forged simply finds no cart. <b>No cookie is set unless a guest cart was
/// created</b> -- an empty-cart read sets nothing.
/// </summary>
public sealed class GuestCartCookieWriter(IGuestCartTokens tokens, IBusinessSettings settings)
{
    /// <summary>The hash of the request's cart cookie, or <c>null</c> when there is none (or it is not a well-formed token).</summary>
    public string? ReadHash(HttpRequest request) => tokens.HashOrNull(request.Cookies[CartRules.GuestCookieName]);

    public void Write(HttpResponse response, string token) =>
        response.Cookies.Append(CartRules.GuestCookieName, token, Options(TimeSpan.FromDays(settings.GetInt(SettingKeys.OrdersGuestCartRetentionDays))));

    /// <summary>Expires the cookie (after a merge): the same attributes with a lifetime of zero.</summary>
    public void Expire(HttpResponse response)
    {
        var options = Options(TimeSpan.Zero);
        options.Expires = DateTimeOffset.UnixEpoch;
        response.Cookies.Append(CartRules.GuestCookieName, string.Empty, options);
    }

    private static CookieOptions Options(TimeSpan lifetime) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = lifetime,
        IsEssential = true,
    };

    /// <summary>A customer session: the caller is a signed-in customer (staff and anonymous callers are guests on a cart route).</summary>
    public static bool IsCustomer(UserContext user) => user.UserId != Guid.Empty && user.Roles.Contains("Customer", StringComparer.Ordinal);
}
