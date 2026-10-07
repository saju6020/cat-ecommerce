using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Account;

public sealed class SetDefaultDeliveryAddressCommand : Command
{
    public required Guid AddressId { get; init; }
}
