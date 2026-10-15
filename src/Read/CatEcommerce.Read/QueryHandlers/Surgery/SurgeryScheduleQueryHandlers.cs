using System.Globalization;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Surgery;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Surgery;

internal static class SurgeryReadFormat
{
    public static MoneyViewModel Money(decimal amount, string currency) => new(amount.ToString("0.00", CultureInfo.InvariantCulture), currency);
}

/// <summary>Active, priced theatre services (US-SRG-01). Two reads: the services, then each one's price in force now (the latest effective one).</summary>
public sealed class GetSurgeryServicesQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetSurgeryServicesQuery, IReadOnlyList<SurgeryServiceViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<SurgeryServiceViewModel>>> HandleAsync(GetSurgeryServicesQuery query)
    {
        var now = timeProvider.GetUtcNow();
        var services = await db.Set<ClinicServiceAggregate>().AsNoTracking()
            .Where(s => !s.IsMarkedToDelete && s.IsActive && s.RequiredRoomKind == RoomKind.Theatre)
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.Category, s.DurationMinutes, s.QualifiedRoleIds })
            .ToListAsync();

        var ids = services.Select(s => s.Id).ToList();
        var prices = await db.Set<ServicePriceAggregate>().AsNoTracking()
            .Where(p => ids.Contains(p.ClinicServiceId) && p.EffectiveFrom <= now)
            .Select(p => new { p.ClinicServiceId, p.EffectiveFrom, p.Amount.Amount, p.Amount.CurrencyCode })
            .ToListAsync();
        var current = prices.GroupBy(p => p.ClinicServiceId).ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.EffectiveFrom).First());

        var result = services.Where(s => current.ContainsKey(s.Id))
            .Select(s => new SurgeryServiceViewModel(
                s.Id, s.Name, s.Category.DisplayName, s.DurationMinutes, SurgeryReadFormat.Money(current[s.Id].Amount, current[s.Id].CurrencyCode),
                s.Category == ServiceCategory.Spay || s.Category == ServiceCategory.Neuter, s.QualifiedRoleIds.ToList()))
            .ToList();

        return new QueryResponse<IReadOnlyList<SurgeryServiceViewModel>> { Result = result };
    }
}

/// <summary>Staff of the branch: surgeons whose roles intersect the service's qualified roles, assistants who are nurse or vet.</summary>
public sealed class GetSurgeryStaffQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetSurgeryStaffQuery, SurgeryStaffViewModel>
{
    public async Task<QueryResponse<SurgeryStaffViewModel>> HandleAsync(GetSurgeryStaffQuery query)
    {
        var qualified = await db.Set<ClinicServiceAggregate>().AsNoTracking().Where(s => s.Id == query.ServiceId).Select(s => s.QualifiedRoleIds).FirstOrDefaultAsync()
            ?? new List<string>();

        var staffIds = await db.Set<StaffBranchAssignmentAggregate>().AsNoTracking()
            .Where(a => a.BranchIds.Contains(query.BranchId)).Select(a => a.Id).ToListAsync();
        var staff = await db.Set<StaffAccountAggregate>().AsNoTracking()
            .Where(s => staffIds.Contains(s.Id) && !s.IsMarkedToDelete)
            .Select(s => new { s.Id, s.FullName, s.RoleIds })
            .ToListAsync();

        SurgeryStaffOptionViewModel Option(Guid id, string name, IReadOnlyList<string> roles) => new(id, name, roles.ToList());

        var surgeons = staff.Where(s => s.RoleIds.Any(r => qualified.Contains(r))).OrderBy(s => s.FullName).Select(s => Option(s.Id, s.FullName, s.RoleIds)).ToList();
        var assistants = staff.Where(s => s.RoleIds.Any(r => SurgeryRules.AssistantRoleIds.Contains(r))).OrderBy(s => s.FullName).Select(s => Option(s.Id, s.FullName, s.RoleIds)).ToList();

        return new QueryResponse<SurgeryStaffViewModel> { Result = new SurgeryStaffViewModel(surgeons, assistants) };
    }
}

/// <summary>US-SRG-01 -- <b>never computes availability in SQL</b>: <c>ISurgerySchedulingInputs</c> loads the facts, <c>SurgerySlotFinder</c> decides. The surgery being rescheduled is excluded
/// through its appointment (<c>excludeSurgeryId</c>).</summary>
public sealed class GetSurgeryAvailabilityQueryHandler(
    ISurgerySchedulingInputs schedulingInputs, IBusinessSettings settings, IAggregateRootRepository<BranchAggregate> branchRepository,
    IAggregateRootRepository<SurgeryAggregate> surgeryRepository, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetSurgeryAvailabilityQuery, SurgeryAvailabilityViewModel>
{
    public async Task<QueryResponse<SurgeryAvailabilityViewModel>> HandleAsync(GetSurgeryAvailabilityQuery query)
    {
        var branch = await branchRepository.GetByIdAsync(query.BranchId);
        var timeZoneId = branch?.TimeZoneId ?? "UTC";
        var gridMinutes = settings.GetInt(SettingKeys.BookingSlotIntervalMinutes, query.BranchId);

        Guid? excludeAppointmentId = null;
        if (query.ExcludeSurgeryId is { } surgeryId)
        {
            var own = await surgeryRepository.GetByIdAsync(surgeryId);
            if (own is not null && own.BranchId == query.BranchId) excludeAppointmentId = own.AppointmentId;
        }

        var inputs = await schedulingInputs.LoadAsync(new SurgeryInputRequest(
            query.BranchId, query.ServiceId, query.Date, query.SurgeonId, query.AssistantId, query.TheatreId, query.CatId, excludeAppointmentId));

        var busy = new SurgeryBusyViewModel(
            Label(inputs.TheatreBusy, inputs.Theatre?.Name ?? "The theatre", timeZoneId),
            Label(inputs.SurgeonBusy, inputs.Surgeon?.Name ?? "The surgeon", timeZoneId),
            Label(inputs.AssistantBusy, inputs.Assistant?.Name ?? "The assistant", timeZoneId));

        var service = inputs.Service;
        if (service is null || !service.IsActive || service.Price is null || service.RequiredRoomKind != RoomKind.Theatre)
            return new QueryResponse<SurgeryAvailabilityViewModel> { Result = new SurgeryAvailabilityViewModel(query.Date, timeZoneId, gridMinutes, [], busy) };

        var starts = SurgerySlotFinder.FindStarts(new SurgerySlotRequest(
            new SurgeryProcedureInfo(service.DurationMinutes, service.QualifiedRoleIds), timeZoneId, inputs.Surgeon, inputs.Assistant, inputs.Theatre,
            inputs.SurgeonBusy, inputs.AssistantBusy, inputs.TheatreBusy, inputs.CatBusy, gridMinutes, timeProvider.GetUtcNow()), query.Date)
            .Select(s => new SurgeryStartViewModel(s.StartsOn, s.EndsOn, s.OutsideOpeningHours)).ToList();

        return new QueryResponse<SurgeryAvailabilityViewModel> { Result = new SurgeryAvailabilityViewModel(query.Date, timeZoneId, gridMinutes, starts, busy) };
    }

    private static IReadOnlyList<SurgeryBusyIntervalViewModel> Label(IReadOnlyList<SurgeryBusyInterval> intervals, string subject, string timeZoneId) =>
        intervals.Select(i => new SurgeryBusyIntervalViewModel(i.Start, i.End, $"{subject} {Describe(i.Kind)} {SurgerySlotFinder.Range(i.Start, i.End, timeZoneId)}")).ToList();

    private static string Describe(SurgeryBusyKind kind) => kind switch
    {
        SurgeryBusyKind.AsAssistant => "is assisting another surgery",
        SurgeryBusyKind.AsSurgeon => "is operating on another surgery",
        SurgeryBusyKind.AsPerformer => "has an appointment",
        _ => "is booked",
    };
}

/// <summary>
/// US-SRG-02 AC2 -- the theatre day list: by theatre then start, each surgery with the checklist items still outstanding BY NAME. Polled, so it is two reads regardless of the
/// number of surgeries: one joined projection (surgery, theatre, surgeon, assistant, cat, customer, appointment flag) and one for the checklist items of those surgeries.
/// </summary>
public sealed class GetSurgeryDayListQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetSurgeryDayListQuery, SurgeryDayListViewModel>
{
    public async Task<QueryResponse<SurgeryDayListViewModel>> HandleAsync(GetSurgeryDayListQuery query)
    {
        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == query.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var rangeStart = AppointmentSlotFinder.InstantOf(query.Date, TimeOnly.MinValue, timeZoneId);
        var rangeEnd = AppointmentSlotFinder.InstantOf(query.Date.AddDays(1), TimeOnly.MinValue, timeZoneId);

        var rows = await (
            from s in db.Surgeries.AsNoTracking()
            where !s.IsMarkedToDelete && s.BranchId == query.BranchId && s.StartsOn >= rangeStart && s.StartsOn < rangeEnd && s.Status != SurgeryStatus.Cancelled
            join room in db.Set<RoomAggregate>().AsNoTracking() on s.TheatreId equals room.Id into rooms
            from room in rooms.DefaultIfEmpty()
            join surgeon in db.Set<StaffAccountAggregate>().AsNoTracking() on s.SurgeonId equals surgeon.Id into surgeons
            from surgeon in surgeons.DefaultIfEmpty()
            join assistant in db.Set<StaffAccountAggregate>().AsNoTracking() on s.AssistantId equals assistant.Id into assistants
            from assistant in assistants.DefaultIfEmpty()
            join cat in db.Set<CatAggregate>().AsNoTracking() on s.CatId equals cat.Id into cats
            from cat in cats.DefaultIfEmpty()
            join customer in db.Set<CustomerAccountAggregate>().AsNoTracking() on s.CustomerId equals customer.Id into customers
            from customer in customers.DefaultIfEmpty()
            join appointment in db.Set<AppointmentAggregate>().AsNoTracking() on s.AppointmentId equals appointment.Id into appointments
            from appointment in appointments.DefaultIfEmpty()
            orderby room.Name, s.StartsOn
            select new Row(
                s.Id, s.Version, s.StartsOn, s.EndsOn, s.AdmitBy, s.Status, s.TheatreId, room != null ? room.Name : "", s.SurgeonId, surgeon != null ? surgeon.FullName : "",
                s.AssistantId, assistant != null ? assistant.FullName : "", s.CatId, cat != null ? cat.Name : "", cat != null && cat.IsHighRisk,
                cat != null && cat.LifeStatus == CatLifeStatus.Deceased, s.CustomerId, customer != null ? customer.FullName : "",
                customer != null && customer.MobileNumber != null ? customer.MobileNumber.Value : null, s.Procedure.ServiceName, s.RecoveryStatus,
                appointment != null && appointment.NeedsRescheduling))
            .ToListAsync();

        var ids = rows.Select(r => r.Id).ToList();
        var items = ids.Count == 0 ? [] : await db.Surgeries.AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .SelectMany(s => s.Checklist.Select(i => new { SurgeryId = s.Id, i.Kind, i.Status }))
            .ToListAsync();
        var bySurgery = items.GroupBy(i => i.SurgeryId).ToDictionary(g => g.Key, g => g.ToList());

        var result = rows.Select(r =>
        {
            var checklist = bySurgery.GetValueOrDefault(r.Id) ?? [];
            var outstanding = PreOpChecklistRules.Order
                .Where(kind => checklist.Any(i => i.Kind == kind && i.Status == ChecklistItemStatus.Outstanding))
                .Select(kind => kind.DisplayName).ToList();
            var consentDone = checklist.Any(i => i.Kind == PreOpChecklistItemKind.ConsentSigned && i.Status == ChecklistItemStatus.Done);

            return new SurgeryDayItemViewModel(
                r.Id, r.Version, r.StartsOn, r.EndsOn, r.AdmitBy, r.Status.DisplayName,
                new SurgeryNamedRefViewModel(r.TheatreId, r.TheatreName), new SurgeryNamedRefViewModel(r.SurgeonId, r.SurgeonName), new SurgeryNamedRefViewModel(r.AssistantId, r.AssistantName),
                new SurgeryDayCatViewModel(r.CatId, r.CatName, r.CatIsHighRisk, r.CatIsDeceased), new SurgeryDayCustomerViewModel(r.CustomerId, r.CustomerName, r.CustomerPhone),
                r.Procedure, new SurgeryChecklistSummaryViewModel(outstanding.Count == 0, outstanding), consentDone, r.NeedsRescheduling, r.RecoveryStatus?.DisplayName);
        }).ToList();

        return new QueryResponse<SurgeryDayListViewModel> { Result = new SurgeryDayListViewModel(query.Date, timeZoneId, result) };
    }

    private sealed record Row(
        Guid Id, int Version, DateTimeOffset StartsOn, DateTimeOffset EndsOn, DateTimeOffset AdmitBy, SurgeryStatus Status, Guid TheatreId, string TheatreName, Guid SurgeonId, string SurgeonName,
        Guid AssistantId, string AssistantName, Guid CatId, string CatName, bool CatIsHighRisk, bool CatIsDeceased, Guid CustomerId, string CustomerName, string? CustomerPhone,
        string Procedure, RecoveryStatus? RecoveryStatus, bool NeedsRescheduling);
}

/// <summary>A paged list of the branch surgeries in a local-date range, filterable by status, surgeon and pricing basis (US-SRG-11 AC2).</summary>
public sealed class GetSurgeryListQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetSurgeryListQuery, PagedResult<SurgerySummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<SurgerySummaryViewModel>>> HandleAsync(GetSurgeryListQuery query)
    {
        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == query.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var filtered = db.Surgeries.AsNoTracking().Where(s => !s.IsMarkedToDelete && s.BranchId == query.BranchId);

        if (query.From is { } from) filtered = filtered.Where(s => s.StartsOn >= AppointmentSlotFinder.InstantOf(from, TimeOnly.MinValue, timeZoneId));
        if (query.To is { } to) filtered = filtered.Where(s => s.StartsOn < AppointmentSlotFinder.InstantOf(to.AddDays(1), TimeOnly.MinValue, timeZoneId));
        if (query.Status is { } statusName && EnumerationParsing.TryParse<SurgeryStatus>(statusName, out var status)) filtered = filtered.Where(s => s.Status == status);
        if (query.SurgeonId is { } surgeonId) filtered = filtered.Where(s => s.SurgeonId == surgeonId);
        if (query.PricingBasis is { } basisName && EnumerationParsing.TryParse<SurgeryPricingBasis>(basisName, out var basis)) filtered = filtered.Where(s => s.PriceSnapshot.Basis == basis);

        // US-SRG-05 / OQ-10: a surgery whose procedure has ended but whose report is not finalised is "report outstanding" (not a discharge blocker -- a filter).
        if (query.ReportOutstanding == true)
            filtered = filtered.Where(s => (s.Status == SurgeryStatus.Recovery || s.Status == SurgeryStatus.Discharged) && !db.SurgicalReports.Any(r => r.Id == s.Id && r.FinalisedOn != null));

        var total = await filtered.CountAsync();
        var rows = await (
            from s in filtered.OrderBy(s => s.StartsOn).ThenBy(s => s.Id).Skip(query.Skip).Take(query.Take)
            join cat in db.Set<CatAggregate>().AsNoTracking() on s.CatId equals cat.Id into cats
            from cat in cats.DefaultIfEmpty()
            join surgeon in db.Set<StaffAccountAggregate>().AsNoTracking() on s.SurgeonId equals surgeon.Id into surgeons
            from surgeon in surgeons.DefaultIfEmpty()
            select new
            {
                s.Id, s.StartsOn, s.EndsOn, s.Status, s.CatId, CatName = cat != null ? cat.Name : "", Procedure = s.Procedure.ServiceName, s.PriceSnapshot.Basis, s.Outcome, s.SurgeonId,
                SurgeonName = surgeon != null ? surgeon.FullName : "",
                ReportFinalised = db.SurgicalReports.Any(r => r.Id == s.Id && r.FinalisedOn != null)
            })
            .ToListAsync();

        var items = rows.Select(r => new SurgerySummaryViewModel(
            r.Id, r.StartsOn, r.EndsOn, r.Status.DisplayName, r.CatId, r.CatName, r.Procedure, r.Basis.DisplayName, r.Outcome?.DisplayName, r.SurgeonId, r.SurgeonName,
            ReportOutstanding: (r.Status == SurgeryStatus.Recovery || r.Status == SurgeryStatus.Discharged) && !r.ReportFinalised, timeZoneId)).ToList();

        return new QueryResponse<PagedResult<SurgerySummaryViewModel>> { Result = new PagedResult<SurgerySummaryViewModel>(items, query.PageNumber, query.PageSize, total) };
    }
}
