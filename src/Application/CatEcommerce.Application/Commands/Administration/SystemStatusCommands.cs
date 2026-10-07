using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Administration;

public sealed class SetMaintenanceModeCommand : Command
{
    public required bool MaintenanceMode { get; init; }
    public string? Message { get; init; }
}
