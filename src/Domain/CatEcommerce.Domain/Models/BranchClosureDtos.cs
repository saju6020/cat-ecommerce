using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

public sealed record BranchClosureCreateDto(
    Guid BranchId, DateOnly StartsOn, DateOnly EndsOn, ClosureKind Kind, string Reason,
    TimeOnly? PartialFrom, TimeOnly? PartialTo, UserContext UserContext, Guid CorrelationId);

/// <summary><paramref name="TodayLocal"/> is the branch-local today, computed by the handler
/// from the branch's time zone (§11.4) -- the aggregate only compares dates, it never converts
/// time zones itself (§20.7).</summary>
public sealed record BranchClosureCancelDto(DateOnly TodayLocal, UserContext UserContext, Guid CorrelationId);
