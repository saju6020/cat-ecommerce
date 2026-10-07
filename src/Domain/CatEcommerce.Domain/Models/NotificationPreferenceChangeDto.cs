using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

public sealed record NotificationPreferenceChangeDto(
    NotificationChannel Channel,
    NotificationCategory Category,
    bool IsGranted);
