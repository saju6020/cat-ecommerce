using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record SetMaintenanceModeDto(
    bool MaintenanceMode, string? Message, DateTimeOffset ChangedOn, UserContext UserContext, Guid CorrelationId);
