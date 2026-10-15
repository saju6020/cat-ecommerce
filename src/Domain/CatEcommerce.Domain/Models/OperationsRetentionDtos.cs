using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-XCT-001 section 8.4. Requests a retention run (scheduled by the worker, or manual by an administrator).</summary>
public sealed record RetentionRunRequestDto(
    RetentionRunTrigger Trigger, Guid RequestedBy, DateTimeOffset RequestedOn, UserContext UserContext, Guid CorrelationId);

public sealed record RetentionRunStartDto(DateTimeOffset StartedOn, UserContext UserContext, Guid CorrelationId);

/// <summary>One finished pass. <paramref name="ErrorCodes"/> are codes, never messages.</summary>
public sealed record RetentionPassDto(
    RetentionPassKind Kind, DateTimeOffset? Cutoff, int Examined, int Affected, int Held, int Failed,
    IReadOnlyList<string> ErrorCodes, int DurationMs, UserContext UserContext, Guid CorrelationId);

public sealed record RetentionRunFinishDto(DateTimeOffset FinishedOn, UserContext UserContext, Guid CorrelationId);
