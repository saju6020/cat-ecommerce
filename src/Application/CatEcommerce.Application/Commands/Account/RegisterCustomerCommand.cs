using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

public sealed class RegisterCustomerCommand : Command
{
    public required Guid CustomerId { get; init; }
    public required string FullName { get; init; }
    public string? MobileNumber { get; init; }
    public string? EmailAddress { get; init; }

    // SECURITY: plaintext. Never logged, never persisted, never echoed.
    public required string Password { get; init; }
}
