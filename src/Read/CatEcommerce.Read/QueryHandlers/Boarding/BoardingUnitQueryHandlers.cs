using System.Globalization;
using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Boarding;

/// <summary>Boarding money for the wire: the amount as an invariant two-decimal string (HO-ADM-001 ADR-054).</summary>
public static class BoardingMoney
{
    public static MoneyViewModel Of(decimal amount, string currencyCode) =>
        new(amount.ToString("0.00", CultureInfo.InvariantCulture), currencyCode);
}

/// <summary>US-BRD-01: the unit read returns the type's current nightly rate (ADR-221).</summary>
public sealed class GetBoardingUnitsQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetBoardingUnitsQuery, IReadOnlyList<BoardingUnitViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<BoardingUnitViewModel>>> HandleAsync(GetBoardingUnitsQuery query)
    {
        var units = await db.BoardingUnits.AsNoTracking()
            .Where(u => u.BranchId == query.BranchId)
            .OrderBy(u => u.Block).ThenBy(u => u.SortOrder).ThenBy(u => u.Code)
            .Select(u => new { u.Id, u.BranchId, u.Code, u.Name, u.Type, u.CapacityCats, u.Block, u.IsActive, u.SortOrder, u.Version })
            .ToListAsync();

        var inForce = await RateCardsInForceAsync(db, query.BranchId, timeProvider.GetUtcNow());

        IReadOnlyList<BoardingUnitViewModel> result = units.Select(u => new BoardingUnitViewModel(
            u.Id, u.BranchId, u.Code, u.Name, u.Type.DisplayName, u.CapacityCats, u.Block, u.IsActive, u.SortOrder,
            inForce.TryGetValue(u.Type.Value, out var card) ? BoardingMoney.Of(card.NightlyRate, card.CurrencyCode) : null, u.Version)).ToList();

        return new QueryResponse<IReadOnlyList<BoardingUnitViewModel>> { Result = result };
    }

    /// <summary>The card in force at <paramref name="now"/> for each unit type: the greatest <c>EffectiveFrom</c> not after it.</summary>
    internal static async Task<Dictionary<int, RateCardRow>> RateCardsInForceAsync(CatEcommerceDbContext db, Guid branchId, DateTimeOffset now)
    {
        var cards = await db.BoardingRateCards.AsNoTracking().Where(c => c.BranchId == branchId && c.EffectiveFrom <= now)
            .Select(c => new RateCardRow(c.Id, c.UnitType, c.NightlyRate, c.WeeklyRate, c.MonthlyRate, c.AdditionalCatNightlyRate, c.CurrencyCode, c.TaxClassId, c.EffectiveFrom, c.SetBy))
            .ToListAsync();
        return cards.GroupBy(c => c.UnitType.Value).ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.EffectiveFrom).First());
    }

    /// <summary>The columns of a rate card a read needs (projected, never the aggregate).</summary>
    internal sealed record RateCardRow(
        Guid Id, BoardingUnitType UnitType, decimal NightlyRate, decimal WeeklyRate, decimal MonthlyRate, decimal AdditionalCatNightlyRate, string CurrencyCode, Guid TaxClassId,
        DateTimeOffset EffectiveFrom, Guid SetBy);
}

public sealed class GetUnitMaintenanceQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetUnitMaintenanceQuery, IReadOnlyList<MaintenanceBlockViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<MaintenanceBlockViewModel>>> HandleAsync(GetUnitMaintenanceQuery query)
    {
        IReadOnlyList<MaintenanceBlockViewModel> result = await db.BoardingMaintenanceBlocks.AsNoTracking()
            .Where(b => b.UnitId == query.UnitId && b.BranchId == query.BranchId)
            .OrderByDescending(b => b.StartsOn)
            .Select(b => new MaintenanceBlockViewModel(b.Id, b.UnitId, b.StartsOn, b.EndsOn, b.Reason, b.IsCancelled))
            .ToListAsync();

        return new QueryResponse<IReadOnlyList<MaintenanceBlockViewModel>> { Result = result };
    }
}

/// <summary>US-BRD-03: history per type, each card marked Current / Scheduled / Past at read time.</summary>
public sealed class GetBoardingRateCardsQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetBoardingRateCardsQuery, IReadOnlyList<BoardingRateCardViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<BoardingRateCardViewModel>>> HandleAsync(GetBoardingRateCardsQuery query)
    {
        var cards = db.BoardingRateCards.AsNoTracking().Where(c => c.BranchId == query.BranchId);
        if (!string.IsNullOrWhiteSpace(query.UnitType) && EnumerationParsing.TryParse<BoardingUnitType>(query.UnitType, out var type) && type is not null)
            cards = cards.Where(c => c.UnitType == type);

        var rows = await cards.OrderBy(c => c.UnitType).ThenByDescending(c => c.EffectiveFrom)
            .Select(c => new GetBoardingUnitsQueryHandler.RateCardRow(c.Id, c.UnitType, c.NightlyRate, c.WeeklyRate, c.MonthlyRate, c.AdditionalCatNightlyRate, c.CurrencyCode, c.TaxClassId, c.EffectiveFrom, c.SetBy))
            .ToListAsync();
        var now = timeProvider.GetUtcNow();
        var currentIds = rows.Where(c => c.EffectiveFrom <= now).GroupBy(c => c.UnitType.Value)
            .Select(g => g.OrderByDescending(c => c.EffectiveFrom).First().Id).ToHashSet();

        IReadOnlyList<BoardingRateCardViewModel> result = rows.Select(c => new BoardingRateCardViewModel(
            c.Id, c.UnitType.DisplayName, BoardingMoney.Of(c.NightlyRate, c.CurrencyCode), BoardingMoney.Of(c.WeeklyRate, c.CurrencyCode),
            BoardingMoney.Of(c.MonthlyRate, c.CurrencyCode), BoardingMoney.Of(c.AdditionalCatNightlyRate, c.CurrencyCode),
            c.TaxClassId, c.EffectiveFrom, c.SetBy,
            currentIds.Contains(c.Id) ? "Current" : c.EffectiveFrom > now ? "Scheduled" : "Past")).ToList();

        return new QueryResponse<IReadOnlyList<BoardingRateCardViewModel>> { Result = result };
    }
}

public sealed class GetBoardingCareRoutineQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetBoardingCareRoutineQuery, BoardingCareRoutineViewModel>
{
    public async Task<QueryResponse<BoardingCareRoutineViewModel>> HandleAsync(GetBoardingCareRoutineQuery query)
    {
        var own = (await db.BoardingCareRoutines.AsNoTracking().Where(r => r.Id == query.BranchId)
                .Select(r => r.Times.Select(t => new { t.Kind, t.LocalTime }).ToList()).FirstOrDefaultAsync())
            ?.Select(t => new RoutineTimeInput(t.Kind, t.LocalTime)).ToList();
        var times = BoardingCareRoutineAggregate.Effective(own)
            .OrderBy(t => t.Kind.Value).ThenBy(t => t.Time)
            .Select(t => new RoutineTimeViewModel(t.Kind.DisplayName, t.Time.ToString("HH:mm", CultureInfo.InvariantCulture)))
            .ToList();

        return new QueryResponse<BoardingCareRoutineViewModel>
        {
            Result = new BoardingCareRoutineViewModel(query.BranchId, own is null || own.Count == 0, times)
        };
    }
}

/// <summary>The public unit-types read: current rates and the largest capacity on offer. A type with no rate card in force, or no active unit, is not listed.</summary>
public sealed class GetBoardingUnitTypesQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetBoardingUnitTypesQuery, IReadOnlyList<BoardingUnitTypeViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<BoardingUnitTypeViewModel>>> HandleAsync(GetBoardingUnitTypesQuery query)
    {
        var branchId = query.BranchId ?? AdministrationSeed.MainBranchId;
        var units = db.BoardingUnits.AsNoTracking().Where(u => u.IsActive && u.BranchId == branchId);

        var capacities = (await units.Select(u => new { u.Type, u.CapacityCats }).ToListAsync())
            .Where(u => u.Type != BoardingUnitType.Isolation) // isolation is never offered online (ADR-227)
            .GroupBy(u => u.Type.Value).ToDictionary(g => g.Key, g => g.Max(u => u.CapacityCats));

        var inForce = await GetBoardingUnitsQueryHandler.RateCardsInForceAsync(db, branchId, timeProvider.GetUtcNow());

        IReadOnlyList<BoardingUnitTypeViewModel> result = capacities
            .Where(c => inForce.ContainsKey(c.Key))
            .OrderBy(c => c.Key)
            .Select(c =>
            {
                var card = inForce[c.Key];
                return new BoardingUnitTypeViewModel(
                    card.UnitType.DisplayName, c.Value, BoardingMoney.Of(card.NightlyRate, card.CurrencyCode), BoardingMoney.Of(card.WeeklyRate, card.CurrencyCode),
                    BoardingMoney.Of(card.MonthlyRate, card.CurrencyCode), BoardingMoney.Of(card.AdditionalCatNightlyRate, card.CurrencyCode));
            }).ToList();

        return new QueryResponse<IReadOnlyList<BoardingUnitTypeViewModel>> { Result = result };
    }
}
