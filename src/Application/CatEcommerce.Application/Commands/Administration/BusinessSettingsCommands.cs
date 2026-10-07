using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Administration;

public sealed class SettingChangeItem
{
    public required string Key { get; init; }
    public required string Value { get; init; }
}

public sealed class ChangeBusinessSettingsCommand : Command
{
    public Guid? BranchId { get; init; }
    public required IReadOnlyList<SettingChangeItem> Changes { get; init; }
}

public sealed class ClearBranchSettingOverrideCommand : Command
{
    public required Guid BranchId { get; init; }
    public required string Key { get; init; }
}
