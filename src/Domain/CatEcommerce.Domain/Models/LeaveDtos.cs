using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record LeaveRequestCreateDto(
    Guid StaffAccountId, DateOnly StartsOn, DateOnly EndsOn, LeaveKind Kind, string? Note,
    UserContext UserContext, Guid CorrelationId);

public sealed record LeaveDecisionDto(
    Guid DeciderId, string? DecisionNote, DateTimeOffset DecidedOn, UserContext UserContext, Guid CorrelationId);

public sealed record LeaveCancelDto(DateOnly Today, Guid CancelledBy, UserContext UserContext, Guid CorrelationId);
