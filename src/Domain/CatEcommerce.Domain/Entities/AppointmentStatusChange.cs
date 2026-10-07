using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-APT-001 §8.2 -- owned by <see cref="Aggregates.Clinic.AppointmentAggregate"/>, one
/// row per transition, never trimmed (C-ALL-4); ordered by <see cref="Sequence"/> within a booking
/// (Appendix C transitions auditable at a glance -- US-CLN-10).</summary>
public sealed class AppointmentStatusChange
{
    public Guid Id { get; private set; }
    public AppointmentStatus From { get; private set; } = default!;
    public AppointmentStatus To { get; private set; } = default!;
    public DateTimeOffset ChangedOn { get; private set; }
    public Guid ChangedBy { get; private set; }

    /// <summary>1-based insertion ordinal within the appointment; the tie-breaker when two
    /// transitions share <see cref="ChangedOn"/>. 0 only on rows written before it existed.</summary>
    public int Sequence { get; private set; }

    private AppointmentStatusChange() { }

    public AppointmentStatusChange(Guid id, AppointmentStatus from, AppointmentStatus to, DateTimeOffset changedOn, Guid changedBy, int sequence = 0)
    {
        Id = id;
        From = from;
        To = to;
        ChangedOn = changedOn;
        ChangedBy = changedBy;
        Sequence = sequence;
    }
}
