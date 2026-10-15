using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Scheduling;

/// <summary>
/// HO-SRG-001 §8.5/§12.2 -- <c>AsNoTracking</c> throughout. Rosters, leave, closures, the service and the rooms come from HO-APT-001's
/// <see cref="IAppointmentSchedulingInputs"/> (staff mode: <c>RestrictToOpeningHours = false</c>, ADR-072); the BUSY intervals are read here, once, from the
/// occupying appointments of the day so that each interval keeps its ROLE for the clash message (performer, surgeon of another surgery, assistant). Held rows whose
/// hold has lapsed never count (HO-APT-001 §8.4), and the surgery's own appointment is excluded on reschedule.
/// </summary>
public sealed class SurgerySchedulingInputs(CatEcommerceDbContext db, IAppointmentSchedulingInputs appointmentInputs, TimeProvider timeProvider) : ISurgerySchedulingInputs
{
    /// <summary>A rostered member of the branch comes from the shared inputs with their availability. A member who is assigned to the branch but has NO roster (a receptionist, say)
    /// is still returned, with their roles and no availability -- so a clash can say "cannot assist in theatre" rather than only "not rostered". Anyone else is null.</summary>
    private async Task<SlotPerformerInput?> StaffAsync(AppointmentSchedulingInputSet shared, Guid staffId, Guid branchId, CancellationToken ct)
    {
        var rostered = shared.Performers.FirstOrDefault(p => p.StaffAccountId == staffId);
        if (rostered is not null) return rostered;

        var assigned = await db.Set<StaffBranchAssignmentAggregate>().AsNoTracking().AnyAsync(a => a.Id == staffId && a.BranchIds.Contains(branchId), ct);
        if (!assigned) return null;

        var account = await db.Set<StaffAccountAggregate>().AsNoTracking().Where(a => a.Id == staffId).Select(a => new { a.Id, a.FullName, a.RoleIds }).FirstOrDefaultAsync(ct);
        return account is null ? null : new SlotPerformerInput(account.Id, account.FullName, account.RoleIds.ToList(), []);
    }

    public async Task<SurgeryInputSet> LoadAsync(SurgeryInputRequest request, CancellationToken ct = default)
    {
        var shared = await appointmentInputs.LoadAsync(new AppointmentSchedulingInputRequest(
            request.BranchId, request.ServiceId, request.LocalDate, RestrictToOpeningHours: false, request.ExcludeAppointmentId, request.CatId, PreferredPerformerId: null), ct);

        var surgeon = request.SurgeonId is { } surgeonId ? await StaffAsync(shared, surgeonId, request.BranchId, ct) : null;
        var assistant = request.AssistantId is { } assistantId ? await StaffAsync(shared, assistantId, request.BranchId, ct) : null;
        var theatre = request.TheatreId is { } theatreId ? shared.Rooms.FirstOrDefault(r => r.RoomId == theatreId) : null;

        if (shared.Service is null && surgeon is null && assistant is null && theatre is null && shared.Rooms.Count == 0)
            return new SurgeryInputSet(shared.TimeZoneId, null, null, null, null, [], [], [], []);

        var rangeStart = AppointmentSlotFinder.InstantOf(request.LocalDate, TimeOnly.MinValue, shared.TimeZoneId);
        var rangeEnd = AppointmentSlotFinder.InstantOf(request.LocalDate.AddDays(1), TimeOnly.MinValue, shared.TimeZoneId);
        var now = timeProvider.GetUtcNow();

        var occupying = db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => a.BranchId == request.BranchId && !a.IsWalkIn && !a.IsMarkedToDelete)
            .Where(a => a.StartsOn != null && a.StartsOn < rangeEnd && a.EndsOn > rangeStart)
            .Where(a => a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.InConsultation
                        || (a.Status == AppointmentStatus.Held && a.HoldExpiresOn > now));

        if (request.ExcludeAppointmentId is { } excludeId)
            occupying = occupying.Where(a => a.Id != excludeId);

        var rows = await occupying
            .Select(a => new { a.PerformerId, a.AssistantId, a.RoomId, a.CatId, a.SurgeryId, StartsOn = a.StartsOn!.Value, EndsOn = a.EndsOn!.Value })
            .ToListAsync(ct);

        IReadOnlyList<SurgeryBusyInterval> StaffBusy(Guid? staffId) => staffId is not { } id ? [] : rows
            .Where(r => r.PerformerId == id || r.AssistantId == id)
            .Select(r => new SurgeryBusyInterval(r.StartsOn, r.EndsOn,
                r.AssistantId == id ? SurgeryBusyKind.AsAssistant : r.SurgeryId is not null ? SurgeryBusyKind.AsSurgeon : SurgeryBusyKind.AsPerformer))
            .OrderBy(i => i.Start)
            .ToList();

        var theatreBusy = request.TheatreId is { } room
            ? rows.Where(r => r.RoomId == room).Select(r => new SurgeryBusyInterval(r.StartsOn, r.EndsOn, SurgeryBusyKind.Booking)).OrderBy(i => i.Start).ToList()
            : [];
        var catBusy = request.CatId is { } cat
            ? rows.Where(r => r.CatId == cat).Select(r => new SurgeryBusyInterval(r.StartsOn, r.EndsOn, SurgeryBusyKind.Booking)).OrderBy(i => i.Start).ToList()
            : [];

        return new SurgeryInputSet(shared.TimeZoneId, shared.Service, surgeon, assistant, theatre, StaffBusy(request.SurgeonId), StaffBusy(request.AssistantId), theatreBusy, catBusy);
    }
}
