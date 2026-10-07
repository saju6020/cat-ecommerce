using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Application.DataMappers;

public static class AddressBookMapper
{
    public static DeliveryAddressAddDto ToAddDto(this AddDeliveryAddressCommand command, DateTimeOffset addedOn) =>
        new(command.AddressId, command.Label, command.Line1, command.Line2, command.City, command.District,
            command.PostalCode, command.DeliveryInstructions, addedOn, command.UserContext, command.CorrelationId);

    public static DeliveryAddressUpdateDto ToUpdateDto(this UpdateDeliveryAddressCommand command, DateTimeOffset changedOn) =>
        new(command.AddressId, command.Label, command.Line1, command.Line2, command.City, command.District,
            command.PostalCode, command.DeliveryInstructions, changedOn, command.UserContext, command.CorrelationId);

    public static DeliveryAddressRemoveDto ToRemoveDto(this RemoveDeliveryAddressCommand command) =>
        new(command.AddressId, command.UserContext, command.CorrelationId);

    public static DeliveryAddressSetDefaultDto ToSetDefaultDto(this SetDefaultDeliveryAddressCommand command) =>
        new(command.AddressId, command.UserContext, command.CorrelationId);
}
