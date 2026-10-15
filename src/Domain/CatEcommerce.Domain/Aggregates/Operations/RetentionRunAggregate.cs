using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Operations;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Operations;

/// <summary>
/// HO-XCT-001 section 8.4 / ADR-303 -- one execution of the retention passes. <c>Requested</c> -> <c>Running</c> -> <c>Succeeded</c> or
/// <c>CompletedWithErrors</c>; each transition is guarded by the status and raises one event, so <c>RetentionRunFinishedEvent</c> gives the audit
/// handler a single place to write the run business-event summary. It is excluded from change capture (it would otherwise write about seven
/// <c>DataChange</c> rows per night); the summary row replaces them. It holds counts and codes only.
/// </summary>
public sealed class RetentionRunAggregate : AggregateRoot
{
    public const int MaxPasses = 16;

    private readonly List<RetentionPassResult> _passes = [];

    public RetentionRunTrigger Trigger { get; private set; } = default!;
    public Guid RequestedBy { get; private set; }
    public DateTimeOffset RequestedOn { get; private set; }
    public DateTimeOffset? StartedOn { get; private set; }
    public DateTimeOffset? FinishedOn { get; private set; }
    public RetentionRunStatus Status { get; private set; } = default!;
    public IReadOnlyList<RetentionPassResult> Passes => _passes;

    private RetentionRunAggregate() { }

    public RetentionRunAggregate(Guid id) : base(id) { }

    public void Request(RetentionRunRequestDto dto)
    {
        if (Status is not null) throw Invalid("This retention run has already been requested.");

        Trigger = dto.Trigger;
        RequestedBy = dto.RequestedBy;
        RequestedOn = dto.RequestedOn;
        Status = RetentionRunStatus.Requested;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new RetentionRunRequestedEvent { RetentionRunId = Id, Trigger = Trigger, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void Start(RetentionRunStartDto dto)
    {
        if (Status != RetentionRunStatus.Requested) throw Invalid("Only a requested retention run can be started.");

        Status = RetentionRunStatus.Running;
        StartedOn = dto.StartedOn;

        AddDomainEvent(new RetentionRunStartedEvent { RetentionRunId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void RecordPass(RetentionPassDto dto)
    {
        if (Status != RetentionRunStatus.Running) throw Invalid("A pass can be recorded only while the retention run is running.");
        if (_passes.Count >= MaxPasses) throw Invalid("A retention run has at most sixteen passes.");
        if (dto.Examined < 0 || dto.Affected < 0 || dto.Held < 0 || dto.Failed < 0 || dto.DurationMs < 0)
            throw Invalid("Pass counts cannot be negative.");

        _passes.Add(new RetentionPassResult(Guid.NewGuid(), dto.Kind, dto.Cutoff, dto.Examined, dto.Affected, dto.Held, dto.Failed, dto.ErrorCodes, dto.DurationMs));

        AddDomainEvent(new RetentionPassRecordedEvent
        {
            RetentionRunId = Id, PassKind = dto.Kind, Affected = dto.Affected, Failed = dto.Failed,
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    public void Finish(RetentionRunFinishDto dto)
    {
        if (Status != RetentionRunStatus.Running) throw Invalid("Only a running retention run can be finished.");

        Status = _passes.Any(p => p.HadErrors) ? RetentionRunStatus.CompletedWithErrors : RetentionRunStatus.Succeeded;
        FinishedOn = dto.FinishedOn;

        AddDomainEvent(new RetentionRunFinishedEvent
        {
            RetentionRunId = Id, Trigger = Trigger, RunStatus = Status, PassCount = _passes.Count,
            TotalAffected = _passes.Sum(p => p.Affected), TotalHeld = _passes.Sum(p => p.Held), TotalFailed = _passes.Sum(p => p.Failed),
            CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    private static DomainRuleViolationException Invalid(string message) => new(BusinessViolationCodes.InvalidRetentionRun, message, nameof(Status));
}
