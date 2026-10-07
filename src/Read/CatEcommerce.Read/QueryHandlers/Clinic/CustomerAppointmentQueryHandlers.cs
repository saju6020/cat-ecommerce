using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Clinic;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Clinic;

public sealed class GetCustomerAppointmentsQueryHandler(CatEcommerceDbContext db, IBusinessSettings businessSettings, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetCustomerAppointmentsQuery, PagedResult<AppointmentSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<AppointmentSummaryViewModel>>> HandleAsync(GetCustomerAppointmentsQuery query)
    {
        var now = timeProvider.GetUtcNow();
        var filtered = db.Set<AppointmentAggregate>().AsNoTracking().Where(a => !a.IsMarkedToDelete && a.CustomerId == query.CustomerId);

        filtered = string.Equals(query.Scope, "past", StringComparison.OrdinalIgnoreCase)
            ? filtered.Where(a => a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.Cancelled || a.Status == AppointmentStatus.NoShow)
            : filtered.Where(a => a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.InConsultation);

        var totalCount = await filtered.CountAsync();

        var ordered = string.Equals(query.Scope, "past", StringComparison.OrdinalIgnoreCase)
            ? filtered.OrderByDescending(a => a.StartsOn)
            : filtered.OrderBy(a => a.StartsOn);

        var rows = await ordered.Skip(query.Skip).Take(query.Take)
            .Select(a => new
            {
                a.Id, a.CatId, a.BranchId, a.PerformerId, a.StartsOn, a.EndsOn, a.Status,
                ServiceName = a.ServiceSnapshot.Name, ServiceCategory = a.ServiceSnapshot.Category, ServiceDuration = a.ServiceSnapshot.DurationMinutes,
                PriceAmount = a.ServiceSnapshot.Price.Amount, PriceCurrency = a.ServiceSnapshot.Price.CurrencyCode,
            })
            .ToListAsync();

        var items = new List<AppointmentSummaryViewModel>();
        foreach (var row in rows)
        {
            var catName = await db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == row.CatId).Select(c => c.Name).FirstOrDefaultAsync() ?? "";
            var branch = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == row.BranchId).Select(b => new { b.Name, b.PhoneNumber, b.TimeZoneId }).FirstOrDefaultAsync();
            var performerName = row.PerformerId is null ? null
                : await db.Set<StaffAccountAggregate>().AsNoTracking().Where(s => s.Id == row.PerformerId).Select(s => s.FullName).FirstOrDefaultAsync();

            var cutoffHours = businessSettings.GetInt(SettingKeys.BookingChangeCutoffHours, row.BranchId);
            var changeCutoffOn = row.StartsOn?.AddHours(-cutoffHours);
            var canChange = row.Status == AppointmentStatus.Booked && changeCutoffOn is not null && now <= changeCutoffOn;

            items.Add(new AppointmentSummaryViewModel(
                row.Id, row.CatId, catName,
                new ServiceSnapshotViewModel(row.ServiceName, row.ServiceCategory.DisplayName, row.ServiceDuration, row.PriceAmount, row.PriceCurrency),
                row.StartsOn, row.EndsOn, performerName,
                new BranchRefViewModel(row.BranchId, branch?.Name ?? "", branch?.PhoneNumber),
                branch?.TimeZoneId ?? "UTC", row.Status.DisplayName, canChange, changeCutoffOn));
        }

        return new QueryResponse<PagedResult<AppointmentSummaryViewModel>> { Result = new PagedResult<AppointmentSummaryViewModel>(items, query.PageNumber, query.PageSize, totalCount) };
    }
}

/// <summary>Returns the caller's own <c>Held</c> appointment too, with <c>holdExpiresOn</c> and
/// <c>isWaitlistOffer</c>, so a waitlist claim link opens it and offers Confirm (§12.4).</summary>
public sealed class GetCustomerAppointmentDetailQueryHandler(CatEcommerceDbContext db, IBusinessSettings businessSettings, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetCustomerAppointmentDetailQuery, CustomerAppointmentDetailViewModel?>
{
    public async Task<QueryResponse<CustomerAppointmentDetailViewModel?>> HandleAsync(GetCustomerAppointmentDetailQuery query)
    {
        var result = await ClinicDetailProjection.BuildCustomerAsync(db, businessSettings, timeProvider, query.AppointmentId, query.CustomerId);
        return new QueryResponse<CustomerAppointmentDetailViewModel?> { Result = result };
    }
}

/// <summary>US-CLN-08 "visible at booking".</summary>
public sealed class GetCustomerAppointmentSummaryQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetCustomerAppointmentSummaryQuery, CustomerAppointmentSummaryViewModel>
{
    public async Task<QueryResponse<CustomerAppointmentSummaryViewModel>> HandleAsync(GetCustomerAppointmentSummaryQuery query)
    {
        var noShowCount = await db.Set<AppointmentAggregate>().AsNoTracking()
            .CountAsync(a => !a.IsMarkedToDelete && a.CustomerId == query.CustomerId && a.Status == AppointmentStatus.NoShow);

        var lastVisitOn = await db.Set<AppointmentAggregate>().AsNoTracking()
            .Where(a => !a.IsMarkedToDelete && a.CustomerId == query.CustomerId && a.Status == AppointmentStatus.Completed)
            .OrderByDescending(a => a.CompletedOn)
            .Select(a => a.CompletedOn)
            .FirstOrDefaultAsync();

        // Read side projects, never rehydrates (.ai/read-side-pattern.md §4): left-joined once
        // instead of one lookup per row, so a missing cat or branch never drops the row (backlog #20).
        var upcomingRows = await (
            from a in db.Set<AppointmentAggregate>().AsNoTracking()
            where !a.IsMarkedToDelete && a.CustomerId == query.CustomerId && a.Status == AppointmentStatus.Booked
            join cat in db.Set<CatAggregate>().AsNoTracking() on a.CatId equals cat.Id into cats
            from cat in cats.DefaultIfEmpty()
            join branch in db.Set<BranchAggregate>().AsNoTracking() on a.BranchId equals branch.Id into branches
            from branch in branches.DefaultIfEmpty()
            orderby a.StartsOn
            select new
            {
                a.Id, a.CatId, CatName = cat != null ? cat.Name : "", a.BranchId, BranchName = branch != null ? branch.Name : "",
                PhoneNumber = branch != null ? branch.PhoneNumber : null, TimeZoneId = branch != null ? branch.TimeZoneId : "UTC",
                a.ServiceSnapshot.Name, a.ServiceSnapshot.Category, a.ServiceSnapshot.DurationMinutes,
                PriceAmount = a.ServiceSnapshot.Price.Amount, PriceCurrency = a.ServiceSnapshot.Price.CurrencyCode,
                a.StartsOn, a.EndsOn, a.Status,
            })
            .Take(AdministrationListCaps.MaxUnboundedListSize)
            .ToListAsync();

        var upcoming = upcomingRows.Select(row => new AppointmentSummaryViewModel(
            row.Id, row.CatId, row.CatName,
            new ServiceSnapshotViewModel(row.Name, row.Category.DisplayName, row.DurationMinutes, row.PriceAmount, row.PriceCurrency),
            row.StartsOn, row.EndsOn, null,
            new BranchRefViewModel(row.BranchId, row.BranchName, row.PhoneNumber), row.TimeZoneId,
            row.Status.DisplayName, false, null)).ToList();

        return new QueryResponse<CustomerAppointmentSummaryViewModel> { Result = new CustomerAppointmentSummaryViewModel(noShowCount, upcoming, lastVisitOn) };
    }
}

public sealed class GetCustomerWaitlistQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetCustomerWaitlistQuery, IReadOnlyList<WaitlistEntryViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<WaitlistEntryViewModel>>> HandleAsync(GetCustomerWaitlistQuery query)
    {
        var rows = await db.Set<WaitlistEntryAggregate>().AsNoTracking()
            .Where(w => !w.IsMarkedToDelete && w.CustomerId == query.CustomerId)
            .OrderByDescending(w => w.CreatedOn)
            .Select(w => new { w.Id, w.BranchId, w.CatId, w.ClinicServiceId, w.LocalDate, w.Status, w.OfferedAppointmentId, w.OfferExpiresOn, w.CreatedOn })
            .ToListAsync();

        var result = new List<WaitlistEntryViewModel>();
        foreach (var row in rows)
        {
            var catName = await db.Set<CatAggregate>().AsNoTracking().Where(c => c.Id == row.CatId).Select(c => c.Name).FirstOrDefaultAsync() ?? "";
            var serviceName = await db.Set<ClinicServiceAggregate>().AsNoTracking().Where(s => s.Id == row.ClinicServiceId).Select(s => s.Name).FirstOrDefaultAsync() ?? "";
            result.Add(new WaitlistEntryViewModel(row.Id, row.BranchId, row.CatId, catName, row.ClinicServiceId, serviceName, row.LocalDate, row.Status.DisplayName, row.OfferedAppointmentId, row.OfferExpiresOn, row.CreatedOn));
        }

        return new QueryResponse<IReadOnlyList<WaitlistEntryViewModel>> { Result = result };
    }
}
