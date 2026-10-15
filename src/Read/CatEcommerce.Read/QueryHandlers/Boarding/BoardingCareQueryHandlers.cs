using System.Globalization;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Boarding;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Boarding;

/// <summary>
/// The day task list, grouped by unit (NFR-AVL-3's input, US-BRD-09/10). Overdue is computed here against the branch's grace setting -- never read from a column. Cat names
/// and handling notes come from the stay snapshot and the cat (a high-risk cat carries its handling note to the tablet).
/// </summary>
public sealed class GetCareTaskDayListQueryHandler(CatEcommerceDbContext db, IBoardingInputs boardingInputs, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetCareTaskDayListQuery, CareTaskDayListViewModel>
{
    public async Task<QueryResponse<CareTaskDayListViewModel>> HandleAsync(GetCareTaskDayListQuery query)
    {
        var settings = await boardingInputs.GetSettingsAsync(query.BranchId);
        var zone = settings?.TimeZoneId ?? "UTC";
        var grace = settings?.TaskOverdueGraceMinutes ?? 30;
        var now = timeProvider.GetUtcNow();

        var tasks = db.BoardingCareTasks.AsNoTracking().Where(t => t.BranchId == query.BranchId && t.LocalDate == query.Date && t.Status != CareTaskStatus.Voided);
        if (query.UnitId is { } unitId) tasks = tasks.Where(t => t.UnitId == unitId);
        if (query.AssignedTo is { } me) tasks = tasks.Where(t => t.AssignedCaretakerId == me);
        if (!string.IsNullOrWhiteSpace(query.Status) && EnumerationParsing.TryParse<CareTaskStatus>(query.Status, out var status) && status is not null) tasks = tasks.Where(t => t.Status == status);

        var rows = await tasks.OrderBy(t => t.DueOn).ThenBy(t => t.CatId)
            .Select(t => new TaskRow(
                t.Id, t.StayId, t.CatId, t.UnitId, t.Kind, t.Title, t.DueOn, t.DueLocalTime, t.Status, t.AssignedCaretakerId, t.CompletedOn, t.CompletedBy, t.Note, t.SkipReason))
            .ToListAsync();
        var stayIds = rows.Select(t => t.StayId).Distinct().ToList();
        var catIds = rows.Select(t => t.CatId).Distinct().ToList();
        var unitIds = rows.Select(t => t.UnitId).Distinct().ToList();
        var caretakerIds = rows.Where(t => t.AssignedCaretakerId != null).Select(t => t.AssignedCaretakerId!.Value).Distinct().ToList();

        var catNames = (await db.BoardingStays.AsNoTracking().Where(s => stayIds.Contains(s.Id)).SelectMany(s => s.Cats).Select(c => new { c.CatId, c.CatName }).ToListAsync())
            .GroupBy(c => c.CatId).ToDictionary(g => g.Key, g => g.First().CatName);
        var catFacts = await db.Set<CatAggregate>().AsNoTracking().Where(c => catIds.Contains(c.Id)).Select(c => new { c.Id, c.IsHighRisk, c.HandlingNote }).ToDictionaryAsync(c => c.Id);
        var units = await db.BoardingUnits.AsNoTracking().Where(u => unitIds.Contains(u.Id)).Select(u => new UnitRow(u.Id, u.Code, u.Name, u.Type, u.Block)).ToDictionaryAsync(u => u.Id);
        var staff = await db.Set<StaffAccountAggregate>().AsNoTracking().Where(s => caretakerIds.Contains(s.Id)).Select(s => new { s.Id, s.FullName }).ToDictionaryAsync(s => s.Id, s => s.FullName);

        var views = rows.Select(t =>
        {
            catFacts.TryGetValue(t.CatId, out var facts);
            return new CareTaskViewModel(
                t.Id, t.StayId, t.CatId, catNames.GetValueOrDefault(t.CatId, ""), facts?.IsHighRisk ?? false, facts?.HandlingNote, t.Kind.DisplayName, t.Title, t.DueOn,
                t.DueLocalTime.ToString("HH:mm", CultureInfo.InvariantCulture), t.Status.DisplayName, CareTaskAggregate.IsOverdue(t.Status, t.DueOn, now, grace), t.AssignedCaretakerId,
                t.AssignedCaretakerId is { } id ? staff.GetValueOrDefault(id) : null, t.CompletedOn, t.CompletedBy, t.Note, t.SkipReason);
        }).ToList();

        var groups = views.GroupBy(v => rows.First(r => r.Id == v.Id).UnitId)
            .Select(g => units.TryGetValue(g.Key, out var u)
                ? new { u.Block, u.Code, Group = new CareTaskUnitGroupViewModel(u.Id, u.Code, u.Name, u.Type.DisplayName, u.Block, g.ToList()) }
                : new { Block = (string?)null, Code = "", Group = new CareTaskUnitGroupViewModel(g.Key, "", "", "", null, g.ToList()) })
            .OrderBy(g => g.Block, StringComparer.Ordinal).ThenBy(g => g.Code, StringComparer.Ordinal)
            .Select(g => g.Group).ToList();

        var result = new CareTaskDayListViewModel(
            query.Date, zone, views.Count, views.Count(v => v.Status == CareTaskStatus.Pending.DisplayName), views.Count(v => v.IsOverdue),
            views.Count(v => v.Status == CareTaskStatus.Pending.DisplayName && v.AssignedCaretakerId is null), groups);

        return new QueryResponse<CareTaskDayListViewModel> { Result = result };
    }
}

internal sealed record TaskRow(
    Guid Id, Guid StayId, Guid CatId, Guid UnitId, CareTaskKind Kind, string Title, DateTimeOffset DueOn, TimeOnly DueLocalTime, CareTaskStatus Status, Guid? AssignedCaretakerId,
    DateTimeOffset? CompletedOn, Guid? CompletedBy, string? Note, string? SkipReason);

internal sealed record UnitRow(Guid Id, string Code, string Name, BoardingUnitType Type, string? Block);

public sealed class GetStayObservationsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStayObservationsQuery, StayObservationsViewModel?>
{
    public async Task<QueryResponse<StayObservationsViewModel?>> HandleAsync(GetStayObservationsQuery query)
    {
        var stay = await db.BoardingStays.AsNoTracking().Where(s => s.Id == query.StayId && s.BranchId == query.BranchId)
            .Select(s => new { s.Id, Cats = s.Cats.Select(c => new { c.CatId, c.CatName }).ToList() }).FirstOrDefaultAsync();
        if (stay is null) return new QueryResponse<StayObservationsViewModel?> { Result = null };

        var observations = await db.BoardingCareObservations.AsNoTracking().Where(o => o.StayId == query.StayId).OrderByDescending(o => o.RecordedOn)
            .Select(o => new { o.Id, o.CatId, o.RecordedOn, o.RecordedBy, o.PortionEaten, o.WaterIntake, o.LitterOutput, o.Mood, o.Notes })
            .ToListAsync();
        var names = stay.Cats.ToDictionary(c => c.CatId, c => c.CatName);

        var notEating = stay.Cats
            .Where(c => NotEatingRule.IsNotEating(observations.Where(o => o.CatId == c.CatId).Select(o => o.PortionEaten).ToList()))
            .Select(c => c.CatId).ToList();

        IReadOnlyList<CareObservationViewModel> items = observations.Select(o => new CareObservationViewModel(
            o.Id, o.CatId, names.GetValueOrDefault(o.CatId, ""), o.RecordedOn, o.RecordedBy, o.PortionEaten.DisplayName, o.WaterIntake.DisplayName, o.LitterOutput.DisplayName, o.Mood.DisplayName, o.Notes)).ToList();

        return new QueryResponse<StayObservationsViewModel?> { Result = new StayObservationsViewModel(stay.Id, notEating, items) };
    }
}

public sealed class GetStaysInHouseQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetStaysInHouseQuery, IReadOnlyList<InHouseStayViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<InHouseStayViewModel>>> HandleAsync(GetStaysInHouseQuery query)
    {
        var stays = await db.BoardingStays.AsNoTracking().Where(s => s.Status == BoardingStayStatus.CheckedIn).Select(s => new { s.Id, s.BranchId }).ToListAsync();
        var branchIds = stays.Select(s => s.BranchId).Distinct().ToList();
        var zones = await db.Set<CatEcommerce.Domain.Aggregates.Administration.BranchAggregate>().AsNoTracking().Where(b => branchIds.Contains(b.Id)).Select(b => new { b.Id, b.TimeZoneId }).ToDictionaryAsync(b => b.Id, b => b.TimeZoneId);

        IReadOnlyList<InHouseStayViewModel> result = stays
            .Select(s => new InHouseStayViewModel(s.Id, BoardingOccupancyWindow.LocalDateOf(query.Now, zones.GetValueOrDefault(s.BranchId, "UTC")))).ToList();
        return new QueryResponse<IReadOnlyList<InHouseStayViewModel>> { Result = result };
    }
}
