using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record RestoreCheckDto(string Name, bool Passed, string? Detail);

/// <summary>
/// HO-XCT-001 section 8.5. The facts of a restore drill. The achieved RPO/RTO and the outcome are NOT here: the aggregate computes them. <paramref name="Today"/>
/// is the main-branch calendar date, so "not in the future" is a clinic-calendar rule.
/// </summary>
public sealed record RestoreTestDto(
    DateOnly PerformedOn, Guid PerformedByStaffId, string Environment, string BackupSetLabel, DateTimeOffset SimulatedFailureAt, DateTimeOffset RecoveredTo,
    DateTimeOffset RestoreStartedAt, DateTimeOffset RestoreCompletedAt, IReadOnlyList<RestoreCheckDto> Checks, string DrillOutput, string? Notes, Guid? SupersedesId,
    DateOnly Today, UserContext UserContext, Guid CorrelationId);
