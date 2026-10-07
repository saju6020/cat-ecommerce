using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Read.Queries.Clinic;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Clinic;

/// <summary>HO-APT-001 §12.6. Never computes availability in SQL.</summary>
public sealed class GetAvailableSlotsQueryHandler(
    IAppointmentSchedulingInputs schedulingInputs, IBusinessSettings businessSettings,
    IAggregateRootRepository<BranchAggregate> branchRepository, IAggregateRootRepository<CatAggregate> catRepository,
    TimeProvider timeProvider)
    : IQueryHandlerAsync<GetAvailableSlotsQuery, AvailableSlotsViewModel>
{
    public async Task<QueryResponse<AvailableSlotsViewModel>> HandleAsync(GetAvailableSlotsQuery query)
    {
        var branch = await branchRepository.GetByIdAsync(query.BranchId);
        var timeZoneId = branch?.TimeZoneId ?? "UTC";

        if (branch is null || !branch.IsActive)
            return Empty(query.Date, timeZoneId);

        var now = timeProvider.GetUtcNow();
        var today = BranchCalendar.LocalDateOf(now, timeZoneId);
        var horizonDays = businessSettings.GetInt(SettingKeys.BookingHorizonDays, query.BranchId);
        if (query.Date < today || query.Date > today.AddDays(horizonDays))
            return Empty(query.Date, timeZoneId);

        // Backlog #24: catId excludes the cat's own bookings, which would let a caller diff the
        // results to learn whether someone else's cat (ids sit on cage-card QR codes) has
        // appointments or holds. Honoured only for a cat the caller owns; any other id is treated
        // as if none was sent, so the response is identical and reveals nothing.
        var catId = await OwnedCatIdAsync(query);

        var inputSet = await schedulingInputs.LoadAsync(new AppointmentSchedulingInputRequest(
            query.BranchId, query.ServiceId, query.Date, RestrictToOpeningHours: true, null, catId, query.PerformerId));

        var service = inputSet.Service;
        if (service is null || !service.IsActive || service.Price is null || !service.IsBookableOnline)
            return Empty(query.Date, timeZoneId);

        var gridMinutes = businessSettings.GetInt(SettingKeys.BookingSlotIntervalMinutes, query.BranchId);
        var minimumNoticeMinutes = businessSettings.GetInt(SettingKeys.BookingMinimumNoticeMinutes, query.BranchId);

        var slots = AppointmentSlotFinder.FindSlots(new SlotRequest(
            new SlotServiceInfo(service.DurationMinutes, service.QualifiedRoleIds, service.RequiredRoomKind),
            query.Date, timeZoneId, inputSet.Performers, inputSet.Rooms, inputSet.Busy, gridMinutes,
            now.AddMinutes(minimumNoticeMinutes), query.PerformerId));

        var grouped = slots
            .GroupBy(s => (s.StartsOn, s.EndsOn))
            .OrderBy(g => g.Key.StartsOn)
            .Select(g => new SlotGroupViewModel(
                g.Key.StartsOn, g.Key.EndsOn,
                g.Select(s => new SlotPerformerViewModel(s.PerformerId, s.PerformerName)).OrderBy(p => p.Name, StringComparer.Ordinal).ToList()))
            .ToList();

        return new QueryResponse<AvailableSlotsViewModel> { Result = new AvailableSlotsViewModel(query.Date, timeZoneId, grouped) };
    }

    private async Task<Guid?> OwnedCatIdAsync(GetAvailableSlotsQuery query)
    {
        if (query.CatId is not { } requestedCatId) return null;

        var cat = await catRepository.GetByIdAsync(requestedCatId);
        return cat is not null && cat.OwnerId == query.UserContext.UserId ? requestedCatId : null;
    }

    private static QueryResponse<AvailableSlotsViewModel> Empty(DateOnly date, string timeZoneId) =>
        new() { Result = new AvailableSlotsViewModel(date, timeZoneId, []) };
}

/// <summary>Staff mode (§8.5 rule 2): shifts outside opening hours count; every slot carries
/// <c>outsideOpeningHours</c>.</summary>
public sealed class GetStaffAvailableSlotsQueryHandler(
    IAppointmentSchedulingInputs schedulingInputs, IBusinessSettings businessSettings,
    IAggregateRootRepository<BranchAggregate> branchRepository, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetStaffAvailableSlotsQuery, IReadOnlyList<StaffSlotViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<StaffSlotViewModel>>> HandleAsync(GetStaffAvailableSlotsQuery query)
    {
        var branch = await branchRepository.GetByIdAsync(query.BranchId);
        var timeZoneId = branch?.TimeZoneId ?? "UTC";
        if (branch is null)
            return new QueryResponse<IReadOnlyList<StaffSlotViewModel>> { Result = [] };

        var inputSet = await schedulingInputs.LoadAsync(new AppointmentSchedulingInputRequest(
            query.BranchId, query.ServiceId, query.Date, RestrictToOpeningHours: false, null, query.CatId, query.PerformerId));

        var service = inputSet.Service;
        if (service is null || !service.IsActive || service.Price is null)
            return new QueryResponse<IReadOnlyList<StaffSlotViewModel>> { Result = [] };

        var gridMinutes = businessSettings.GetInt(SettingKeys.BookingSlotIntervalMinutes, query.BranchId);
        var now = timeProvider.GetUtcNow();

        var slots = AppointmentSlotFinder.FindSlots(new SlotRequest(
            new SlotServiceInfo(service.DurationMinutes, service.QualifiedRoleIds, service.RequiredRoomKind),
            query.Date, timeZoneId, inputSet.Performers, inputSet.Rooms, inputSet.Busy, gridMinutes, now, query.PerformerId));

        var result = slots
            .Select(s => new StaffSlotViewModel(s.StartsOn, s.EndsOn, s.PerformerId, s.PerformerName, s.RoomId, s.RoomName, s.OutsideOpeningHours))
            .ToList();

        return new QueryResponse<IReadOnlyList<StaffSlotViewModel>> { Result = result };
    }
}

/// <summary>Staff holding a qualified role and assigned to the branch -- the "choose a vet" filter.</summary>
public sealed class GetBookingPerformersQueryHandler(
    IAppointmentSchedulingInputs schedulingInputs, IAggregateRootRepository<BranchAggregate> branchRepository, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetBookingPerformersQuery, IReadOnlyList<BookingPerformerViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<BookingPerformerViewModel>>> HandleAsync(GetBookingPerformersQuery query)
    {
        // CODE_REVIEW gate fix (2026-09-28, MEDIUM, fixed alongside the waitlist HIGH): branch-local
        // "today" (ADR-045), not UTC -- a mismatch near midnight in a non-UTC branch could list or
        // omit a performer based on the wrong day's roster.
        var branch = await branchRepository.GetByIdAsync(query.BranchId);
        var today = BranchCalendar.LocalDateOf(timeProvider.GetUtcNow(), branch?.TimeZoneId ?? "UTC");
        var inputSet = await schedulingInputs.LoadAsync(new AppointmentSchedulingInputRequest(query.BranchId, query.ServiceId, today, true, null, null, null));

        if (inputSet.Service is null)
            return new QueryResponse<IReadOnlyList<BookingPerformerViewModel>> { Result = [] };

        var result = inputSet.Performers
            .Where(p => p.RoleIds.Any(r => inputSet.Service.QualifiedRoleIds.Contains(r, StringComparer.Ordinal)))
            .Select(p => new BookingPerformerViewModel(p.StaffAccountId, p.Name))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        return new QueryResponse<IReadOnlyList<BookingPerformerViewModel>> { Result = result };
    }
}
