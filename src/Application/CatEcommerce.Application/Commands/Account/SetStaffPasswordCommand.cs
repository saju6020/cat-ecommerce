using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

/// <summary>Owner, 2026-10-19: an administrator sets a staff member's password (there is no email delivery for the
/// self-service reset in production). Ends the staff member's open sessions (the security stamp rotates).</summary>
public sealed class SetStaffPasswordCommand : Command
{
    public required Guid StaffAccountId { get; init; }

    // SECURITY: plaintext. Never logged, never persisted, never echoed.
    public required string NewPassword { get; init; }
}
