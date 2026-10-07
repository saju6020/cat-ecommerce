using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record CustomerAddressBookOpenDto(UserContext UserContext, Guid CorrelationId);
