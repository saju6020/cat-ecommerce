using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-APT-001 §8.7 -- one branch queue entry (a <c>CheckedIn</c> or <c>InConsultation</c>
/// appointment). <paramref name="Status"/> is one of those two.</summary>
public sealed record QueueEntryInput(
    Guid AppointmentId,
    TriagePriority Priority,
    bool IsWalkIn,
    DateTimeOffset? StartsOn,
    DateTimeOffset? CheckedInOn,
    Guid? PerformerId,
    int DurationMinutes,
    AppointmentStatus Status,
    DateTimeOffset? StartedOn);
