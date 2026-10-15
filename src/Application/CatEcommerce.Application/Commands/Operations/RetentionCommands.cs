using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Operations;

/// <summary>HO-XCT-001 section 11.1 C5 -- requests a retention run. <c>RetentionRunId</c> is minted by the caller (the controller, or the worker
/// for a scheduled run). Refused while another run is Requested or Running.</summary>
public sealed class RequestRetentionRunCommand : Command
{
    public required Guid RetentionRunId { get; init; }

    /// <summary>A <c>RetentionRunTrigger</c> display name.</summary>
    public required string Trigger { get; init; }
}

/// <summary>C6 -- the worker moves a Requested run to Running.</summary>
public sealed class StartRetentionRunCommand : Command
{
    public required Guid RetentionRunId { get; init; }
}

/// <summary>C6 -- the worker records one finished pass on the run. Counts and codes only.</summary>
public sealed class RecordRetentionPassCommand : Command
{
    public required Guid RetentionRunId { get; init; }

    /// <summary>A <c>RetentionPassKind</c> display name.</summary>
    public required string PassKind { get; init; }

    public DateTimeOffset? Cutoff { get; init; }
    public int Examined { get; init; }
    public int Affected { get; init; }
    public int Held { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<string> ErrorCodes { get; init; } = [];
    public int DurationMs { get; init; }
}

/// <summary>C6 -- the worker finishes the run; its status is computed from the passes.</summary>
public sealed class FinishRetentionRunCommand : Command
{
    public required Guid RetentionRunId { get; init; }
}

/// <summary>C7 -- one batch of a purge pass. <c>PassKind</c> names the table class (PurgeSessions, PurgeResetTokens, PurgeWebVitals, PurgeBackupRuns); the
/// handler picks the matching <c>IRetentionPurger</c> method. Returns the number of rows deleted.</summary>
public sealed class PurgeExpiredRecordsCommand : Command
{
    public required string PassKind { get; init; }
    public required DateTimeOffset Cutoff { get; init; }
    public required int BatchSize { get; init; }
}

/// <summary>C8 -- one batch of one registered <c>IRetentionPurgeSource</c> (for example boarding media). Returns its <c>PurgeResult</c>.</summary>
public sealed class RunPurgeSourceCommand : Command
{
    public required string SourceKey { get; init; }
    public required DateTimeOffset Cutoff { get; init; }
    public required int BatchSize { get; init; }
}

/// <summary>C9 -- redacts the content of one notification, or the variables of one notification request, past
/// <c>retention.notificationContentMonths</c> (ADR-305). Exactly one id is set.</summary>
public sealed class RedactExpiredNotificationContentCommand : Command
{
    public Guid? NotificationId { get; init; }
    public Guid? RequestId { get; init; }
}
