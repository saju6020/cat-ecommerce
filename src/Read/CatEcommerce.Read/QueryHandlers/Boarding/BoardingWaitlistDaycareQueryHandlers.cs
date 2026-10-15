using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Boarding;

public sealed class GetCustomerWaitlistQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetCustomerWaitlistQuery, IReadOnlyList<BoardingWaitlistEntryViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<BoardingWaitlistEntryViewModel>>> HandleAsync(GetCustomerWaitlistQuery query)
    {
        var rows = await db.BoardingWaitlistEntries.AsNoTracking().Where(e => e.CustomerId == query.CustomerId).OrderByDescending(e => e.JoinedOn).Take(100)
            .Select(e => new { e.Id, e.BranchId, e.ArrivalOn, e.DepartureOn, e.CatCount, e.UnitType, e.Status, e.JoinedOn, e.NotifiedOn })
            .ToListAsync();
        IReadOnlyList<BoardingWaitlistEntryViewModel> result = rows
            .Select(e => new BoardingWaitlistEntryViewModel(e.Id, e.BranchId, e.ArrivalOn, e.DepartureOn, e.CatCount, e.UnitType?.DisplayName, e.Status.DisplayName, e.JoinedOn, e.NotifiedOn)).ToList();
        return new QueryResponse<IReadOnlyList<BoardingWaitlistEntryViewModel>> { Result = result };
    }
}

internal sealed record DaycareRow(
    Guid Id, Guid BranchId, Guid CustomerId, Guid CatId, string CatName, DateOnly LocalDate, int SlotNumber, DaycareBasis Basis, int? ExpectedHours, DaycareVisitStatus Status,
    DateTimeOffset? CheckedInOn, DateTimeOffset? CheckedOutOn, decimal? ChargeAmount, string CurrencyCode)
{
    public DaycareVisitViewModel ToViewModel(string? customerName) => new(
        Id, BranchId, CustomerId, customerName, CatId, CatName, LocalDate, SlotNumber, Basis.DisplayName, ExpectedHours, Status.DisplayName, CheckedInOn, CheckedOutOn,
        ChargeAmount is { } amount ? BoardingMoney.Of(amount, CurrencyCode) : null);
}

public sealed class GetDaycareDayQueryHandler(CatEcommerceDbContext db, IBusinessSettings settings) : IQueryHandlerAsync<GetDaycareDayQuery, DaycareDayViewModel>
{
    public async Task<QueryResponse<DaycareDayViewModel>> HandleAsync(GetDaycareDayQuery query)
    {
        var capacity = settings.GetInt(SettingKeys.BoardingDaycareDailyCapacity, query.BranchId);
        var visits = await db.DaycareVisits.AsNoTracking().Where(v => v.BranchId == query.BranchId && v.LocalDate == query.Date && v.Status != DaycareVisitStatus.Cancelled).OrderBy(v => v.SlotNumber)
            .Select(v => new DaycareRow(v.Id, v.BranchId, v.CustomerId, v.CatId, v.CatName, v.LocalDate, v.SlotNumber, v.Basis, v.ExpectedHours, v.Status, v.CheckedInOn, v.CheckedOutOn, v.ChargeAmount, v.CurrencyCode))
            .ToListAsync();
        var customerIds = visits.Select(v => v.CustomerId).Distinct().ToList();
        var names = await db.Set<CustomerAccountAggregate>().AsNoTracking().Where(c => customerIds.Contains(c.Id)).Select(c => new { c.Id, c.FullName }).ToDictionaryAsync(c => c.Id, c => c.FullName);

        IReadOnlyList<DaycareVisitViewModel> rows = visits.Select(v => v.ToViewModel(names.GetValueOrDefault(v.CustomerId))).ToList();
        return new QueryResponse<DaycareDayViewModel> { Result = new DaycareDayViewModel(query.Date, capacity, visits.Count, capacity > 0, rows) };
    }
}

public sealed class GetCustomerDaycareQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetCustomerDaycareQuery, IReadOnlyList<DaycareVisitViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<DaycareVisitViewModel>>> HandleAsync(GetCustomerDaycareQuery query)
    {
        var visits = await db.DaycareVisits.AsNoTracking().Where(v => v.CustomerId == query.CustomerId).OrderByDescending(v => v.LocalDate).Take(100)
            .Select(v => new DaycareRow(v.Id, v.BranchId, v.CustomerId, v.CatId, v.CatName, v.LocalDate, v.SlotNumber, v.Basis, v.ExpectedHours, v.Status, v.CheckedInOn, v.CheckedOutOn, v.ChargeAmount, v.CurrencyCode))
            .ToListAsync();
        IReadOnlyList<DaycareVisitViewModel> rows = visits.Select(v => v.ToViewModel(null)).ToList();
        return new QueryResponse<IReadOnlyList<DaycareVisitViewModel>> { Result = rows };
    }
}

public sealed class GetDaycareRateCardsQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider) : IQueryHandlerAsync<GetDaycareRateCardsQuery, IReadOnlyList<DaycareRateCardViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<DaycareRateCardViewModel>>> HandleAsync(GetDaycareRateCardsQuery query)
    {
        var now = timeProvider.GetUtcNow();
        var cards = await db.DaycareRateCards.AsNoTracking().Where(c => c.BranchId == query.BranchId).OrderByDescending(c => c.EffectiveFrom)
            .Select(c => new { c.Id, c.HourlyRate, c.DayRate, c.CurrencyCode, c.TaxClassId, c.EffectiveFrom })
            .ToListAsync();
        var current = cards.FirstOrDefault(c => c.EffectiveFrom <= now)?.Id;

        IReadOnlyList<DaycareRateCardViewModel> rows = cards.Select(c => new DaycareRateCardViewModel(
            c.Id, BoardingMoney.Of(c.HourlyRate, c.CurrencyCode), BoardingMoney.Of(c.DayRate, c.CurrencyCode), c.TaxClassId, c.EffectiveFrom, c.EffectiveFrom > now ? "Scheduled" : c.Id == current ? "Current" : "Past")).ToList();
        return new QueryResponse<IReadOnlyList<DaycareRateCardViewModel>> { Result = rows };
    }
}

public sealed class GetWaitlistCandidatesQueryHandler(IBoardingWaitlistDaycareFinder finder) : IQueryHandlerAsync<GetWaitlistCandidatesQuery, IReadOnlyList<Guid>>
{
    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetWaitlistCandidatesQuery query) =>
        new() { Result = await finder.FindWaitingEntriesAsync() };
}

public sealed class GetStaleWaitlistEntriesQueryHandler(IBoardingWaitlistDaycareFinder finder) : IQueryHandlerAsync<GetStaleWaitlistEntriesQuery, IReadOnlyList<Guid>>
{
    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetStaleWaitlistEntriesQuery query) =>
        new() { Result = await finder.FindWaitingEntriesArrivingBeforeAsync(DateOnly.FromDateTime(query.Now.UtcDateTime).AddDays(1)) };
}

public sealed class GetDailyUpdateCandidatesQueryHandler(IBoardingWaitlistDaycareFinder finder, IBusinessSettings settings) : IQueryHandlerAsync<GetDailyUpdateCandidatesQuery, IReadOnlyList<DailyUpdateCandidateViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<DailyUpdateCandidateViewModel>>> HandleAsync(GetDailyUpdateCandidatesQuery query)
    {
        var hours = new Dictionary<Guid, int>();
        int HourOf(Guid branchId) => hours.TryGetValue(branchId, out var h) ? h : hours[branchId] = settings.GetInt(SettingKeys.BoardingDailyUpdateHour, branchId);

        IReadOnlyList<DailyUpdateCandidateViewModel> result = (await finder.FindDailyUpdateCandidatesAsync(query.Now, HourOf)).Select(c => new DailyUpdateCandidateViewModel(c.StayId, c.LocalDate)).ToList();
        return new QueryResponse<IReadOnlyList<DailyUpdateCandidateViewModel>> { Result = result };
    }
}
