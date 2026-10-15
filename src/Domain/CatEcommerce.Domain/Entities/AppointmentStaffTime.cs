using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-SRG-001 §8.4 (ADR-159) -- one row per staff member an appointment occupies: the performer and, for a surgery, the assistant. An exclusion
/// constraint cannot compare two different columns, so "the nurse assisting at 10:00 is not also vaccinating at 10:00" is only a database guarantee if
/// both roles sit in ONE column of ONE table. Owned by <see cref="Aggregates.Clinic.AppointmentAggregate"/> and rewritten only by its private
/// <c>SyncStaffTime()</c>; a row is updated in place (never deleted and re-inserted) so an unchanged occupancy never meets its own old row.
/// </summary>
public sealed class AppointmentStaffTime
{
    public Guid Id { get; private set; }
    public StaffTimeRole Role { get; private set; } = default!;
    public Guid StaffAccountId { get; private set; }
    public DateTimeOffset StartsOn { get; private set; }
    public DateTimeOffset EndsOn { get; private set; }

    /// <summary>Mirrors HO-APT-001 §8.4 occupying predicate: status Held, Booked, CheckedIn or InConsultation, and not a walk-in.</summary>
    public bool Occupies { get; private set; }

    private AppointmentStaffTime() { }

    public AppointmentStaffTime(Guid id, StaffTimeRole role, Guid staffAccountId, DateTimeOffset startsOn, DateTimeOffset endsOn, bool occupies)
    {
        Id = id;
        Role = role;
        StaffAccountId = staffAccountId;
        StartsOn = startsOn;
        EndsOn = endsOn;
        Occupies = occupies;
    }

    public void Update(Guid staffAccountId, DateTimeOffset startsOn, DateTimeOffset endsOn, bool occupies)
    {
        StaffAccountId = staffAccountId;
        StartsOn = startsOn;
        EndsOn = endsOn;
        Occupies = occupies;
    }
}
