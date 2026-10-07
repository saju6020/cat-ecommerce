using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Clinic;

/// <summary>HO-APT-001 §12.6 -- the staff and customer detail queries share one row load, but each
/// maps to its own view model (backlog #26): the customer's never carries staff-only fields. The
/// staff view's <c>allowedTransitions[]</c> always comes from <c>AppointmentTransitionRules</c>, the
/// same table the aggregate uses. Projects with <c>Select</c> (.ai/read-side-pattern.md §4 rules
/// 1-2) -- never rehydrates the aggregate to build the view. Soft-deleted rows are never returned
/// (backlog #9).</summary>
internal static class ClinicDetailProjection
{
    /// <summary>Staff route: narrowed to the caller's active branch (HO-ADM-001 §8.2, backlog #8) --
    /// another branch's appointment is simply not found.</summary>
    public static async Task<AppointmentDetailViewModel?> BuildStaffAsync(
        CatEcommerceDbContext db, IBusinessSettings businessSettings, TimeProvider timeProvider,
        Guid appointmentId, Guid branchId)
    {
        var query = db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => a.Id == appointmentId && !a.IsMarkedToDelete && a.BranchId == branchId);

        var loaded = await LoadAsync(db, businessSettings, timeProvider, query);
        if (loaded is null) return null;
        var (row, ctx) = loaded.Value;

        return new AppointmentDetailViewModel(
            row.Id, row.BranchId, ctx.TimeZoneId, row.CustomerId, row.CatId, ctx.CatName,
            new ServiceSnapshotViewModel(row.ServiceName, row.ServiceCategory.DisplayName, row.ServiceDurationMinutes, row.PriceAmount, row.PriceCurrencyCode),
            row.PerformerId, ctx.PerformerName, row.RoomId, ctx.RoomName, row.StartsOn, row.EndsOn,
            row.IsWalkIn, row.Channel.DisplayName, row.Status.DisplayName, row.HoldExpiresOn,
            row.WaitlistEntryId is not null, row.ReasonForVisit, row.TriagePriority.DisplayName,
            row.PresentingComplaint, row.Version, row.NeedsRescheduling, row.RescheduleFlagReason?.DisplayName,
            ctx.CanChange, ctx.ChangeCutoffOn,
            row.StatusHistory.Select(h => new AppointmentStatusChangeViewModel(h.From.DisplayName, h.To.DisplayName, h.ChangedOn, h.ChangedBy)).ToList(),
            AppointmentTransitionRules.AllowedActions(row.Status));
    }

    /// <summary>Customer route: narrowed to the caller's own appointment.</summary>
    public static async Task<CustomerAppointmentDetailViewModel?> BuildCustomerAsync(
        CatEcommerceDbContext db, IBusinessSettings businessSettings, TimeProvider timeProvider,
        Guid appointmentId, Guid customerId)
    {
        var query = db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => a.Id == appointmentId && !a.IsMarkedToDelete && a.CustomerId == customerId);

        var loaded = await LoadAsync(db, businessSettings, timeProvider, query);
        if (loaded is null) return null;
        var (row, ctx) = loaded.Value;

        return new CustomerAppointmentDetailViewModel(
            row.Id, row.BranchId, ctx.TimeZoneId, row.CatId, ctx.CatName,
            new ServiceSnapshotViewModel(row.ServiceName, row.ServiceCategory.DisplayName, row.ServiceDurationMinutes, row.PriceAmount, row.PriceCurrencyCode),
            row.PerformerId, ctx.PerformerName, row.StartsOn, row.EndsOn,
            row.Status.DisplayName, row.HoldExpiresOn, row.WaitlistEntryId is not null, row.ReasonForVisit,
            row.NeedsRescheduling, row.RescheduleFlagReason?.DisplayName, ctx.CanChange, ctx.ChangeCutoffOn,
            row.StatusHistory.Select(h => new CustomerAppointmentStatusChangeViewModel(h.From.DisplayName, h.To.DisplayName, h.ChangedOn)).ToList());
    }

    private static async Task<(AppointmentDetailRow Row, DetailContext Context)?> LoadAsync(
        CatEcommerceDbContext db, IBusinessSettings businessSettings, TimeProvider timeProvider,
        IQueryable<AppointmentAggregate> query)
    {
        var row = await query
            .Select(a => new AppointmentDetailRow(
                a.Id, a.BranchId, a.CustomerId, a.CatId,
                a.ServiceSnapshot.Name, a.ServiceSnapshot.Category, a.ServiceSnapshot.DurationMinutes,
                a.ServiceSnapshot.Price.Amount, a.ServiceSnapshot.Price.CurrencyCode,
                a.PerformerId, a.RoomId, a.StartsOn, a.EndsOn, a.IsWalkIn, a.Channel, a.Status,
                a.HoldExpiresOn, a.WaitlistEntryId, a.ReasonForVisit, a.TriagePriority, a.PresentingComplaint,
                a.Version, a.NeedsRescheduling, a.RescheduleFlagReason,
                a.StatusHistory.OrderBy(h => h.ChangedOn).ThenBy(h => h.Sequence)
                    .Select(h => new StatusChangeRow(h.From, h.To, h.ChangedOn, h.ChangedBy))
                    .ToList()))
            .FirstOrDefaultAsync();

        if (row is null) return null;

        var branch = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == row.BranchId)
            .Select(b => new { b.TimeZoneId }).FirstOrDefaultAsync();
        var catName = await db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == row.CatId).Select(c => c.Name).FirstOrDefaultAsync() ?? "";
        var performerName = row.PerformerId is null ? null
            : await db.Set<StaffAccountAggregate>().AsNoTracking().Where(s => s.Id == row.PerformerId).Select(s => s.FullName).FirstOrDefaultAsync();
        var roomName = row.RoomId is null ? null
            : await db.Set<RoomAggregate>().AsNoTracking().Where(r => r.Id == row.RoomId).Select(r => r.Name).FirstOrDefaultAsync();

        var now = timeProvider.GetUtcNow();
        var cutoffHours = businessSettings.GetInt(SettingKeys.BookingChangeCutoffHours, row.BranchId);
        var changeCutoffOn = row.StartsOn?.AddHours(-cutoffHours);
        var canChange = row.Status == AppointmentStatus.Booked && changeCutoffOn is not null && now <= changeCutoffOn;

        return (row, new DetailContext(branch?.TimeZoneId ?? "UTC", catName, performerName, roomName, canChange, changeCutoffOn));
    }

    private sealed record DetailContext(
        string TimeZoneId, string CatName, string? PerformerName, string? RoomName, bool CanChange, DateTimeOffset? ChangeCutoffOn);

    private sealed record StatusChangeRow(AppointmentStatus From, AppointmentStatus To, DateTimeOffset ChangedOn, Guid ChangedBy);

    private sealed record AppointmentDetailRow(
        Guid Id, Guid BranchId, Guid CustomerId, Guid CatId,
        string ServiceName, ServiceCategory ServiceCategory, int ServiceDurationMinutes,
        decimal PriceAmount, string PriceCurrencyCode,
        Guid? PerformerId, Guid? RoomId, DateTimeOffset? StartsOn, DateTimeOffset? EndsOn, bool IsWalkIn,
        BookingChannel Channel, AppointmentStatus Status, DateTimeOffset? HoldExpiresOn, Guid? WaitlistEntryId,
        string? ReasonForVisit, TriagePriority TriagePriority, string? PresentingComplaint, int Version,
        bool NeedsRescheduling, RescheduleFlagReason? RescheduleFlagReason,
        List<StatusChangeRow> StatusHistory);
}
