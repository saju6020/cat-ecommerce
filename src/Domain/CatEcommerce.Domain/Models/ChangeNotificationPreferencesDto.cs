using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record ChangeNotificationPreferencesDto(
    IReadOnlyList<NotificationPreferenceChangeDto> Changes,
    string Source,
    DateTimeOffset RecordedOn,
    UserContext UserContext,
    Guid CorrelationId);
