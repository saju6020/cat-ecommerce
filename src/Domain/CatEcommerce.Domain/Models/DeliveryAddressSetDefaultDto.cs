using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record DeliveryAddressSetDefaultDto(Guid AddressId, UserContext UserContext, Guid CorrelationId);
