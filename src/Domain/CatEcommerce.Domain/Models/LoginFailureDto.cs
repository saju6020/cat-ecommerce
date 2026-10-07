using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record LoginFailureDto(DateTimeOffset AsOf, UserContext UserContext, Guid CorrelationId);
