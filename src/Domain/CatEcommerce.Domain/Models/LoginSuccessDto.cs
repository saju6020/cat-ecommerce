using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record LoginSuccessDto(DateTimeOffset AsOf, UserContext UserContext, Guid CorrelationId);
