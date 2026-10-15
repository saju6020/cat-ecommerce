using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-EMR-001 §8.2/§8.9 -- the follow-up set on a visit: either a link to an appointment booked
/// through HO-APT-001 (<see cref="FollowUpKind.Appointment"/>) or a queued reminder
/// (<see cref="FollowUpKind.Reminder"/>). <see cref="Stamp"/> changes on every set; the queued
/// reminder carries a copy and <c>VisitFollowUpGuard</c> compares them, so a changed or cleared
/// follow-up stops the old reminder without deleting its row (ADR-131).
/// </summary>
public sealed record FollowUp
{
    public FollowUpKind Kind { get; init; }
    public DateOnly DueOn { get; init; }
    public Guid? AppointmentId { get; init; }
    public string Stamp { get; init; }
    public DateTimeOffset SetOn { get; init; }
    public Guid SetBy { get; init; }

    public FollowUp(FollowUpKind kind, DateOnly dueOn, Guid? appointmentId, string stamp, DateTimeOffset setOn, Guid setBy)
    {
        Kind = kind;
        DueOn = dueOn;
        AppointmentId = appointmentId;
        Stamp = stamp;
        SetOn = setOn;
        SetBy = setBy;
    }

    // EF only.
    private FollowUp()
    {
        Kind = default!;
        Stamp = default!;
    }
}
