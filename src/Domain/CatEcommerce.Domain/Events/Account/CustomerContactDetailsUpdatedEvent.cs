using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Account;

public sealed class CustomerContactDetailsUpdatedEvent : Event
{
    public required Guid CustomerId { get; init; }
    public required bool NameChanged { get; init; }
    public required bool EmailChanged { get; init; }
    public required bool AdditionalPhoneNumbersChanged { get; init; }
}
