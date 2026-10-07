using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record CustomerNotificationPreferencesOpenDto(UserContext UserContext, Guid CorrelationId);
