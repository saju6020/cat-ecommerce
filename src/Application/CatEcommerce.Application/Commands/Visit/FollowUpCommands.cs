using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Visit;

// HO-EMR-001 §11.1 C18 (Phase G).

/// <summary>Sets (or replaces) the follow-up of an open visit: a link to an appointment booked through HO-APT-001
/// (<c>Appointment</c>), or a reminder queued with a send-time guard (<c>Reminder</c>). Every set mints a new stamp, so
/// a reminder queued for the previous follow-up stands down (ADR-131).</summary>
public sealed class SetFollowUpCommand : Command
{
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Kind { get; init; }
    public required DateOnly DueOn { get; init; }
    public Guid? AppointmentId { get; init; }
}

public sealed class ClearFollowUpCommand : Command
{
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
}
