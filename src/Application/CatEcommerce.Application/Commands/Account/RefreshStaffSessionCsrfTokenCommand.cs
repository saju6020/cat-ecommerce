using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

/// <summary>HO-ACC-001 §12.5 Amended (18) / ADR-342 (owner decision A). Dispatched by
/// <c>SessionAuthenticationMiddleware</c> on a safe (GET) request when the resolved staff
/// session's <c>pc_csrf</c> cookie is missing or does not hash to the session's stored value.</summary>
public sealed class RefreshStaffSessionCsrfTokenCommand : Command
{
    public required string TokenHash { get; init; }
}
