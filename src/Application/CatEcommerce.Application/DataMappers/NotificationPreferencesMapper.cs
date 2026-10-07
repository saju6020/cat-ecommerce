using CatEcommerce.Application.Commands.Account;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Application.DataMappers;

public static class NotificationPreferencesMapper
{
    public static ChangeNotificationPreferencesDto ToChangeDto(
        this ChangeNotificationPreferencesCommand command,
        IReadOnlyList<NotificationPreferenceChangeDto> changes,
        DateTimeOffset recordedOn,
        string source) =>
        new(changes, source, recordedOn, command.UserContext, command.CorrelationId);
}
