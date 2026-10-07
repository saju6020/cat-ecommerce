using System.Text.Json.Serialization;

namespace CatEcommerce.Read.ViewModels;

public sealed record StaffSessionStatusViewModel(
    Guid StaffAccountId,
    string FullName,
    IReadOnlyList<string> RoleIds,
    DateTimeOffset IssuedOn,
    DateTimeOffset LastActivityOn,
    DateTimeOffset IdleExpiresOn,
    DateTimeOffset AbsoluteExpiresOn,
    int IdleTimeoutMinutes,
    DateTimeOffset ServerTime)
{
    /// <summary>See <see cref="CustomerSessionViewModel.CsrfTokenHash"/> -- same reasoning.
    /// <c>JsonIgnore</c>d because <c>GET /api/staff/session</c> (<c>StaffSessionController</c>)
    /// serialises this whole record straight to the response body.</summary>
    [JsonIgnore]
    public string CsrfTokenHash { get; init; } = default!;
}
