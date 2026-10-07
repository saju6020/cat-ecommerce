using System.Text.Json.Serialization;

namespace CatEcommerce.Read.ViewModels;

public sealed record CustomerSessionViewModel(Guid CustomerId, string FullName, DateTimeOffset ExpiresOn, string RecordStatus)
{
    /// <summary>
    /// HO-ACC-001 §12.5 Amended (18) / ADR-342. Carried on this ViewModel so
    /// <c>SessionAuthenticationMiddleware</c> can validate and refresh the CSRF token from the
    /// same query that resolves the session, without a second round trip. <c>JsonIgnore</c>d
    /// because <c>GET /api/auth/session</c> (<c>AuthController.GetSession</c>) serialises this
    /// whole record straight to the response body -- a hash of the CSRF token must never leave
    /// the process in a JSON payload, even though it is not the raw secret itself.
    /// </summary>
    [JsonIgnore]
    public string CsrfTokenHash { get; init; } = default!;
}
