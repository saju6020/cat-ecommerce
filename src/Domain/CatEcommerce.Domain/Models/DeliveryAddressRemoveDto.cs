using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record DeliveryAddressRemoveDto(Guid AddressId, UserContext UserContext, Guid CorrelationId);
