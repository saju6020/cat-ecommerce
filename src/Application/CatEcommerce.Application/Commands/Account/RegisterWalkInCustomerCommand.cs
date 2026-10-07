using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

public sealed class RegisterWalkInCustomerCommand : Command
{
    public required Guid CustomerId { get; init; }
    public required string FullName { get; init; }
    public required string MobileNumber { get; init; }
}
