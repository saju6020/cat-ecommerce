using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Scheduling;

/// <summary>HO-APT-001 §8.12/§12.2. <c>AsNoTracking</c> throughout; one round trip per input set.
/// Calls HO-ADM-001's <see cref="IBranchCalendar"/> and the pure <see cref="StaffAvailability"/>.</summary>
public sealed class AppointmentSchedulingInputs(CatEcommerceDbContext db, IBranchCalendar branchCalendar, TimeProvider timeProvider)
    : IAppointmentSchedulingInputs
{
    public async Task<AppointmentSchedulingInputSet> LoadAsync(AppointmentSchedulingInputRequest request, CancellationToken ct = default)
    {
        var branch = await db.Set<BranchAggregate>().AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BranchId, ct);
        if (branch is null)
            return new AppointmentSchedulingInputSet("UTC", null, [], [], SlotBusyIntervals.Empty);

        var service = await LoadServiceAsync(request.ServiceId, ct);

        var rooms = await db.Set<RoomAggregate>().AsNoTracking()
            .Where(r => r.BranchId == request.BranchId)
            .Select(r => new SlotRoomInput(r.Id, r.Name, r.Kind, r.IsActive))
            .ToListAsync(ct);

        var staffIds = await db.Set<StaffBranchAssignmentAggregate>().AsNoTracking()
            .Where(a => a.BranchIds.Contains(request.BranchId))
            .Select(a => a.Id)
            .ToListAsync(ct);

        var openIntervals = await branchCalendar.OpenIntervalsAsync(request.BranchId, request.LocalDate, request.LocalDate, ct);

        var performers = new List<SlotPerformerInput>();
        foreach (var staffId in staffIds)
        {
            var staff = await db.Set<StaffAccountAggregate>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == staffId, ct);
            var roster = await db.Set<StaffRosterAggregate>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.StaffAccountId == staffId && r.BranchId == request.BranchId, ct);
            if (staff is null || roster is null) continue;

            var exceptions = await db.Set<RosterExceptionAggregate>().AsNoTracking()
                .Where(e => e.StaffAccountId == staffId && e.BranchId == request.BranchId && !e.IsCancelled && e.Date == request.LocalDate)
                .ToListAsync(ct);

            var approvedLeave = await db.Set<LeaveRequestAggregate>().AsNoTracking()
                .Where(l => l.StaffAccountId == staffId && l.Status == LeaveStatus.Approved && l.StartsOn <= request.LocalDate && request.LocalDate <= l.EndsOn)
                .ToListAsync(ct);

            var intervals = StaffAvailability.Intervals(
                roster, exceptions, approvedLeave, openIntervals, branch.TimeZoneId,
                request.LocalDate, request.LocalDate, request.RestrictToOpeningHours);

            var windows = intervals.Select(i => new SlotAvailabilityWindow(i.Start, i.End, i.OutsideOpeningHours)).ToList();
            performers.Add(new SlotPerformerInput(staff.Id, staff.FullName, staff.RoleIds, windows));
        }

        var rangeStart = AppointmentSlotFinder.InstantOf(request.LocalDate, TimeOnly.MinValue, branch.TimeZoneId);
        var rangeEnd = AppointmentSlotFinder.InstantOf(request.LocalDate.AddDays(1), TimeOnly.MinValue, branch.TimeZoneId);
        var now = timeProvider.GetUtcNow();

        var occupying = db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => a.BranchId == request.BranchId && !a.IsWalkIn)
            .Where(a => a.StartsOn != null && a.StartsOn < rangeEnd && a.EndsOn > rangeStart)
            .Where(a =>
                a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.InConsultation ||
                (a.Status == AppointmentStatus.Held && a.HoldExpiresOn > now));

        if (request.ExcludeAppointmentId is { } excludeId)
            occupying = occupying.Where(a => a.Id != excludeId);

        var occupyingRows = await occupying
            .Select(a => new { a.Id, a.PerformerId, a.AssistantId, a.RoomId, a.CatId, a.StartsOn, a.EndsOn })
            .ToListAsync(ct);

        // HO-SRG-001 section 11.7 G4 (ADR-159): a staff member is busy as performer OR as assistant, so a nurse assisting a surgery is not offered ordinary slots.
        // Read from the appointment columns that clinic.appointment_staff_time mirrors (rather than the table itself) so a LAPSED hold -- which the
        // `occupying` predicate above excludes but the table's occupies flag does not -- never makes anyone look busy.
        var staffBusyRows = occupyingRows.Where(a => a.PerformerId is not null).Select(a => (StaffId: a.PerformerId!.Value, a.StartsOn, a.EndsOn))
            .Concat(occupyingRows.Where(a => a.AssistantId is not null).Select(a => (StaffId: a.AssistantId!.Value, a.StartsOn, a.EndsOn)));

        var byPerformer = staffBusyRows
            .GroupBy(a => a.StaffId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TimeInterval>)g.Select(a => new TimeInterval(a.StartsOn!.Value, a.EndsOn!.Value)).ToList());

        var byRoom = occupyingRows.Where(a => a.RoomId is not null)
            .GroupBy(a => a.RoomId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TimeInterval>)g.Select(a => new TimeInterval(a.StartsOn!.Value, a.EndsOn!.Value)).ToList());

        var catBusy = request.CatId is { } catId
            ? occupyingRows.Where(a => a.CatId == catId).Select(a => new TimeInterval(a.StartsOn!.Value, a.EndsOn!.Value)).ToList()
            : [];

        var busy = new SlotBusyIntervals(byPerformer, byRoom, catBusy);

        return new AppointmentSchedulingInputSet(branch.TimeZoneId, service, performers, rooms, busy);
    }

    private async Task<SchedulingServiceInfo?> LoadServiceAsync(Guid serviceId, CancellationToken ct)
    {
        var service = await db.Set<ClinicServiceAggregate>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId, ct);
        if (service is null) return null;

        var now = timeProvider.GetUtcNow();
        var price = await db.Set<ServicePriceAggregate>().AsNoTracking()
            .Where(p => p.ClinicServiceId == serviceId && p.EffectiveFrom <= now)
            .OrderByDescending(p => p.EffectiveFrom)
            .Select(p => p.Amount)
            .FirstOrDefaultAsync(ct);

        return new SchedulingServiceInfo(
            service.Name, service.Category, service.DurationMinutes, service.QualifiedRoleIds,
            service.RequiredRoomKind, service.IsActive, service.IsBookableOnline, price);
    }
}
