using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record StaffSessionIdleTimeoutDto(
    DateTimeOffset AsOf,
    TimeSpan IdleTimeout,
    UserContext UserContext,
    Guid CorrelationId);
