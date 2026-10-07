using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record SettingChangeInput(string Key, string Value);

public sealed record BusinessSettingsChangeDto(
    IReadOnlyList<SettingChangeInput> Changes, UserContext UserContext, Guid CorrelationId);

public sealed record BusinessSettingsClearOverrideDto(string Key, UserContext UserContext, Guid CorrelationId);
