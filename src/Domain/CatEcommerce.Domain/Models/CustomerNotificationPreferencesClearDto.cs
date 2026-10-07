using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>Anonymisation only (§8.10a).</summary>
public sealed record CustomerNotificationPreferencesClearDto(UserContext UserContext, Guid CorrelationId);
