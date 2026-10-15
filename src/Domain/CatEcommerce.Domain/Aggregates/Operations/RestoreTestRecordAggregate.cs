using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Operations;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Operations;

/// <summary>
/// HO-XCT-001 section 8.5 / ADR-313. The record of one restore drill. Append-only and one-shot: <see cref="Record"/> works once, no other public mutator
/// exists, and a correction is a new record that names the one it supersedes. The achieved RPO, the achieved RTO and the outcome are <b>computed here</b>
/// from the facts; the caller never supplies them, so a drill cannot be recorded as passing when its own numbers say otherwise.
/// </summary>
public sealed class RestoreTestRecordAggregate : AggregateRoot
{
    public const int MaxEnvironmentLength = 100;
    public const int MaxBackupSetLabelLength = 200;
    public const int MaxDrillOutputLength = 20_000;
    public const int MaxNotesLength = 2_000;

    private readonly List<RestoreCheckResult> _checks = [];

    public DateOnly PerformedOn { get; private set; }
    public Guid PerformedByStaffId { get; private set; }
    public string Environment { get; private set; } = default!;
    public string BackupSetLabel { get; private set; } = default!;
    public DateTimeOffset SimulatedFailureAt { get; private set; }
    public DateTimeOffset RecoveredTo { get; private set; }
    public DateTimeOffset RestoreStartedAt { get; private set; }
    public DateTimeOffset RestoreCompletedAt { get; private set; }
    public decimal AchievedRpoMinutes { get; private set; }
    public decimal AchievedRtoMinutes { get; private set; }
    public RestoreTestOutcome Outcome { get; private set; } = default!;

    /// <summary>The drill script output, pasted. Stored as text and never rendered as markup.</summary>
    public string DrillOutput { get; private set; } = default!;

    public string? Notes { get; private set; }
    public Guid? SupersedesId { get; private set; }
    public IReadOnlyList<RestoreCheckResult> Checks => _checks;

    private RestoreTestRecordAggregate() { }

    public RestoreTestRecordAggregate(Guid id) : base(id) { }

    public void Record(RestoreTestDto dto)
    {
        if (Outcome is not null) throw Violation(BusinessViolationCodes.RestoreTestAlreadyRecorded, OperationsMessages.RestoreTestAlreadyRecorded);
        if (dto.PerformedOn > dto.Today) throw Violation(BusinessViolationCodes.InvalidRestoreTestTimes, OperationsMessages.InvalidRestoreTestTimes);
        if (dto.RecoveredTo > dto.SimulatedFailureAt) throw Violation(BusinessViolationCodes.InvalidRestoreTestTimes, OperationsMessages.InvalidRestoreTestTimes);
        if (dto.RestoreCompletedAt <= dto.RestoreStartedAt) throw Violation(BusinessViolationCodes.InvalidRestoreTestTimes, OperationsMessages.InvalidRestoreTestTimes);
        if (dto.Checks.Count == 0) throw Violation(BusinessViolationCodes.RestoreTestChecksRequired, OperationsMessages.RestoreTestChecksRequired);

        PerformedOn = dto.PerformedOn;
        PerformedByStaffId = dto.PerformedByStaffId;
        Environment = dto.Environment;
        BackupSetLabel = dto.BackupSetLabel;
        SimulatedFailureAt = dto.SimulatedFailureAt;
        RecoveredTo = dto.RecoveredTo;
        RestoreStartedAt = dto.RestoreStartedAt;
        RestoreCompletedAt = dto.RestoreCompletedAt;
        DrillOutput = dto.DrillOutput;
        Notes = dto.Notes;
        SupersedesId = dto.SupersedesId;
        foreach (var check in dto.Checks) _checks.Add(new RestoreCheckResult(Guid.NewGuid(), check.Name, check.Passed, check.Detail));

        AchievedRpoMinutes = Minutes(dto.SimulatedFailureAt - dto.RecoveredTo);
        AchievedRtoMinutes = Minutes(dto.RestoreCompletedAt - dto.RestoreStartedAt);
        Outcome = _checks.All(c => c.Passed) && AchievedRpoMinutes <= RecoveryObjectives.RpoMinutes && AchievedRtoMinutes <= RecoveryObjectives.RtoMinutes
            ? RestoreTestOutcome.Passed
            : RestoreTestOutcome.Failed;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new RestoreTestRecordedEvent
        {
            RestoreTestId = Id, TestOutcome = Outcome, PerformedOn = PerformedOn, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    private static decimal Minutes(TimeSpan span) => decimal.Round((decimal)span.TotalMinutes, 2);

    private static DomainRuleViolationException Violation(string code, string message) => new(code, message, nameof(Outcome));
}
