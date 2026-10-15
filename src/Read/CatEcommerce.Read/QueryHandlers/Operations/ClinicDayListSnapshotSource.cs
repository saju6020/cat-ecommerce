using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Operations;

/// <summary>
/// HO-XCT-001 section 8.11 -- a section of the offline front-desk snapshot. Registered as multiple Scoped services of this interface and consumed as
/// <c>IEnumerable</c>. A source returns the section body, or <c>null</c> to be omitted.
/// </summary>
public interface IFrontDeskSnapshotSource
{
    string SectionKey { get; }

    /// <summary>The action the caller needs to receive this section, resolved against <c>ActionRoles</c>; <c>null</c> for no extra requirement.</summary>
    string? RequiredAction { get; }

    Task<object?> BuildAsync(Guid branchId, DateOnly localDate, TimeZoneInfo zone, CancellationToken ct);
}

/// <summary>
/// HO-XCT-001 section 8.11 -- today's appointments for the branch: Booked, CheckedIn, InConsultation, Completed and NoShow, with walk-ins in the queue; never
/// Held, Cancelled or expired. Joined to the cat, the customer, the service snapshot name, the performer and the room by explicit projection. The local
/// day is decided by <see cref="BranchCalendar.LocalDateOf"/> (never <c>DateTime.Today</c>).
/// </summary>
public sealed class ClinicDayListSnapshotSource(CatEcommerceDbContext db) : IFrontDeskSnapshotSource
{
    public const string Key = "appointments";

    private static readonly AppointmentStatus[] IncludedStatuses =
        [AppointmentStatus.Booked, AppointmentStatus.CheckedIn, AppointmentStatus.InConsultation, AppointmentStatus.Completed, AppointmentStatus.NoShow];

    public string SectionKey => Key;
    public string? RequiredAction => Actions.Appointments.View;

    public async Task<object?> BuildAsync(Guid branchId, DateOnly localDate, TimeZoneInfo zone, CancellationToken ct)
    {
        // A coarse window in SQL (one day either side of the local date), then the exact local-date decision in memory.
        var windowStart = new DateTimeOffset(localDate.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(localDate.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var rows = await db.Appointments.AsNoTracking()
            .Where(a => a.BranchId == branchId && IncludedStatuses.Contains(a.Status)
                && ((a.StartsOn != null && a.StartsOn >= windowStart && a.StartsOn < windowEnd)
                    || (a.IsWalkIn && a.CheckedInOn != null && a.CheckedInOn >= windowStart && a.CheckedInOn < windowEnd)))
            .Select(a => new
            {
                a.Id, a.StartsOn, a.EndsOn, a.CheckedInOn, a.Status, a.IsWalkIn, a.TriagePriority, a.CatId, a.CustomerId, a.PerformerId, a.RoomId,
                ServiceName = a.ServiceSnapshot.Name,
            })
            .ToListAsync(ct);

        var today = rows.Where(a => BranchCalendar.LocalDateOf(a.StartsOn ?? a.CheckedInOn!.Value, zone.Id) == localDate).ToList();
        if (today.Count == 0) return new SnapshotAppointmentsSection([]);

        var catIds = today.Select(a => a.CatId).Distinct().ToList();
        var customerIds = today.Select(a => a.CustomerId).Distinct().ToList();
        var performerIds = today.Where(a => a.PerformerId != null).Select(a => a.PerformerId!.Value).Distinct().ToList();
        var roomIds = today.Where(a => a.RoomId != null).Select(a => a.RoomId!.Value).Distinct().ToList();

        var cats = await db.Set<CatAggregate>().AsNoTracking().Where(c => catIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.IsHighRisk, c.HandlingNote }).ToDictionaryAsync(c => c.Id, ct);
        var customers = await db.CustomerAccounts.AsNoTracking().Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.FullName, c.MobileNumber }).ToDictionaryAsync(c => c.Id, ct);
        var performers = await db.StaffAccounts.AsNoTracking().Where(s => performerIds.Contains(s.Id))
            .Select(s => new { s.Id, s.FullName }).ToDictionaryAsync(s => s.Id, s => s.FullName, ct);
        var rooms = await db.Rooms.AsNoTracking().Where(r => roomIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Name }).ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        var items = today
            .OrderBy(a => a.StartsOn ?? a.CheckedInOn).ThenBy(a => a.Id)
            .Select(a =>
            {
                cats.TryGetValue(a.CatId, out var cat);
                customers.TryGetValue(a.CustomerId, out var customer);
                return new SnapshotAppointmentItem(
                    a.Id, a.StartsOn, a.EndsOn, a.Status.DisplayName, a.IsWalkIn,
                    a.IsWalkIn ? a.TriagePriority.DisplayName : null,
                    cat?.Name ?? string.Empty, cat?.IsHighRisk ?? false,
                    cat is { IsHighRisk: true } ? cat.HandlingNote : null,
                    customer?.FullName ?? string.Empty, customer?.MobileNumber?.Value,
                    a.ServiceName,
                    a.PerformerId is { } p && performers.TryGetValue(p, out var performerName) ? performerName : null,
                    a.RoomId is { } r && rooms.TryGetValue(r, out var roomName) ? roomName : null);
            }).ToList();

        return new SnapshotAppointmentsSection(items);
    }
}
