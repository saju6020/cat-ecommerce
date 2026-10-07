using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record DeliveryAddressAddDto(
    Guid AddressId,
    string Label,
    string Line1,
    string? Line2,
    string City,
    string District,
    string? PostalCode,
    string? DeliveryInstructions,
    DateTimeOffset AddedOn,
    UserContext UserContext,
    Guid CorrelationId);
