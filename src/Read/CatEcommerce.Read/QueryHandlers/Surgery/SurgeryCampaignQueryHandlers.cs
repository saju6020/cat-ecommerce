using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Surgery;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Surgery;

internal static class SurgeryCampaignReads
{
    /// <summary>Non-cancelled surgeries booked under each campaign (the cap and the price lock both count these).</summary>
    public static async Task<Dictionary<Guid, int>> CountsAsync(CatEcommerceDbContext db, IReadOnlyCollection<Guid> campaignIds)
    {
        var rows = await db.Surgeries.AsNoTracking()
            .Where(s => !s.IsMarkedToDelete && s.PriceSnapshot.CampaignId != null && campaignIds.Contains(s.PriceSnapshot.CampaignId.Value) && s.Status != SurgeryStatus.Cancelled)
            .GroupBy(s => s.PriceSnapshot.CampaignId!.Value)
            .Select(g => new { CampaignId = g.Key, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(r => r.CampaignId, r => r.Count);
    }
}

/// <summary>HO-SRG-001 section 12.6 -- the campaign list with service names and case counts: the campaigns (projected, one page), then the names of the priced services, then one grouped count.</summary>
public sealed class GetSurgeryCampaignsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetSurgeryCampaignsQuery, PagedResult<SurgeryCampaignViewModel>>
{
    public async Task<QueryResponse<PagedResult<SurgeryCampaignViewModel>>> HandleAsync(GetSurgeryCampaignsQuery query)
    {
        var filtered = db.SurgeryCampaigns.AsNoTracking().Where(c => !c.IsMarkedToDelete && (!query.ActiveOnly || c.IsActive));
        var total = await filtered.CountAsync();
        var campaigns = await filtered.OrderByDescending(c => c.ValidFrom).ThenByDescending(c => c.CreatedDate).ThenBy(c => c.Id).Skip(query.Skip).Take(query.Take)
            .Select(c => new
            {
                c.Id, c.Version, c.Code, c.Name, c.Description, c.ValidFrom, c.ValidTo, c.BranchIds, c.MaxCases, c.IsActive,
                Prices = c.Prices.Select(p => new { p.ServiceId, Amount = p.Price.Amount, Currency = p.Price.CurrencyCode }).ToList()
            }).ToListAsync();

        var serviceIds = campaigns.SelectMany(c => c.Prices.Select(p => p.ServiceId)).Distinct().ToList();
        var names = serviceIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<ClinicServiceAggregate>().AsNoTracking().Where(s => serviceIds.Contains(s.Id)).Select(s => new { s.Id, s.Name }).ToDictionaryAsync(s => s.Id, s => s.Name);
        var counts = await SurgeryCampaignReads.CountsAsync(db, campaigns.Select(c => c.Id).ToList());

        IReadOnlyList<SurgeryCampaignViewModel> items = campaigns.Select(c =>
        {
            var booked = counts.GetValueOrDefault(c.Id);
            return new SurgeryCampaignViewModel(
                c.Id, c.Version, c.Code, c.Name, c.Description, c.ValidFrom, c.ValidTo, c.BranchIds.ToList(),
                c.Prices.Select(p => new SurgeryCampaignPriceViewModel(p.ServiceId, names.GetValueOrDefault(p.ServiceId, ""), SurgeryReadFormat.Money(p.Amount, p.Currency))).ToList(),
                c.MaxCases, booked, c.MaxCases is { } cap ? Math.Max(0, cap - booked) : null, c.IsActive, booked > 0);
        }).ToList();
        return new QueryResponse<PagedResult<SurgeryCampaignViewModel>> { Result = new PagedResult<SurgeryCampaignViewModel>(items, query.PageNumber, query.PageSize, total) };
    }
}

/// <summary>The campaigns reception may offer for this booking. The same <c>Evaluate</c> the booking uses decides (with a placeholder eligibility note, since the note is the caller's).</summary>
public sealed class GetApplicableCampaignsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetApplicableCampaignsQuery, IReadOnlyList<ApplicableCampaignViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<ApplicableCampaignViewModel>>> HandleAsync(GetApplicableCampaignsQuery query)
    {
        var candidates = await db.SurgeryCampaigns.AsNoTracking()
            .Where(c => !c.IsMarkedToDelete && c.IsActive && c.ValidFrom <= query.Date && c.ValidTo >= query.Date && c.Prices.Any(p => p.ServiceId == query.ServiceId))
            .OrderBy(c => c.Code)
            .Select(c => new
            {
                c.Id, c.Code, c.Name, c.ValidFrom, c.ValidTo, c.MaxCases, c.BranchIds,
                Price = c.Prices.Where(p => p.ServiceId == query.ServiceId).Select(p => new { Amount = p.Price.Amount, Currency = p.Price.CurrencyCode }).FirstOrDefault()
            }).ToListAsync();
        var counts = await SurgeryCampaignReads.CountsAsync(db, candidates.Select(c => c.Id).ToList());

        // The booking's own decision (SurgeryCampaignAggregate.Evaluate), applied to the projected columns, with a placeholder note since the note is the caller's.
        var result = new List<ApplicableCampaignViewModel>();
        foreach (var c in candidates)
        {
            var booked = counts.GetValueOrDefault(c.Id);
            var price = c.Price is null ? null : new Money(c.Price.Amount, c.Price.Currency);
            var application = SurgeryCampaignAggregate.Evaluate(true, c.ValidFrom, c.ValidTo, c.BranchIds, price, c.MaxCases, query.BranchId, query.Date, "placeholder", booked);
            if (!application.Applies) continue;

            result.Add(new ApplicableCampaignViewModel(
                c.Id, c.Code, c.Name, c.ValidFrom, c.ValidTo, SurgeryReadFormat.Money(application.Price!.Amount, application.Price.CurrencyCode), c.MaxCases is { } cap ? Math.Max(0, cap - booked) : null));
        }

        return new QueryResponse<IReadOnlyList<ApplicableCampaignViewModel>> { Result = result };
    }
}

/// <summary>
/// US-SRG-11 -- the cases of a campaign, one page of them. The totals cover EVERY case in the range, so they come from a narrow projection of all of them (six columns, no rows
/// rehydrated); the page itself is projected, with the cats' names and the standard prices of the services involved. The comparison is made in memory.
/// </summary>
public sealed class GetCampaignCasesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetCampaignCasesQuery, CampaignCasesViewModel?>
{
    public async Task<QueryResponse<CampaignCasesViewModel?>> HandleAsync(GetCampaignCasesQuery query)
    {
        var campaign = await db.SurgeryCampaigns.AsNoTracking().Where(c => c.Id == query.CampaignId && !c.IsMarkedToDelete).Select(c => new { c.Id, c.Code, c.Name }).FirstOrDefaultAsync();
        if (campaign is null) return new QueryResponse<CampaignCasesViewModel?> { Result = null };

        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking().Where(b => b.Id == query.BranchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync() ?? "UTC";
        var surgeries = db.Surgeries.AsNoTracking().Where(s => !s.IsMarkedToDelete && s.BranchId == query.BranchId && s.PriceSnapshot.CampaignId == query.CampaignId);
        if (query.From is { } from) surgeries = surgeries.Where(s => s.StartsOn >= AppointmentSlotFinder.InstantOf(from, TimeOnly.MinValue, timeZoneId));
        if (query.To is { } to) surgeries = surgeries.Where(s => s.StartsOn < AppointmentSlotFinder.InstantOf(to.AddDays(1), TimeOnly.MinValue, timeZoneId));

        var forTotals = await surgeries
            .Select(s => new { s.Id, s.ClinicServiceId, s.CreatedDate, s.Status, Amount = s.PriceSnapshot.Amount.Amount, Currency = s.PriceSnapshot.Amount.CurrencyCode })
            .ToListAsync();

        var rows = await surgeries.OrderBy(s => s.StartsOn).ThenBy(s => s.Id).Skip(query.Skip).Take(query.Take)
            .Select(s => new
            {
                s.Id, s.CatId, s.ClinicServiceId, s.StartsOn, s.Status, s.Outcome, s.CreatedDate, Procedure = s.Procedure.ServiceName, s.PriceSnapshot.CampaignEligibilityNote,
                Amount = s.PriceSnapshot.Amount.Amount, Currency = s.PriceSnapshot.Amount.CurrencyCode
            }).ToListAsync();

        var catIds = rows.Select(r => r.CatId).Distinct().ToList();
        var catNames = catIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<CatAggregate>().AsNoTracking().Where(c => catIds.Contains(c.Id)).Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name);

        var serviceIds = forTotals.Select(r => r.ClinicServiceId).Distinct().ToList();
        var prices = serviceIds.Count == 0
            ? []
            : await db.Set<ServicePriceAggregate>().AsNoTracking().Where(p => serviceIds.Contains(p.ClinicServiceId)).Select(p => new { p.ClinicServiceId, p.EffectiveFrom, p.Amount.Amount, p.Amount.CurrencyCode }).ToListAsync();

        // The standard price at the moment the surgery was booked: the latest price effective on or before its creation (HO-ADM-001 prices are append-only).
        var pricesByService = prices.GroupBy(p => p.ClinicServiceId).ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.EffectiveFrom).ToList());
        var standardAt = (Guid serviceId, DateTimeOffset createdDate) =>
            pricesByService.TryGetValue(serviceId, out var list) ? list.FirstOrDefault(p => p.EffectiveFrom <= createdDate) : null;

        var cases = rows.Select(r =>
        {
            var standard = standardAt(r.ClinicServiceId, r.CreatedDate);
            return new CampaignCaseViewModel(
                r.Id, r.CatId, catNames.GetValueOrDefault(r.CatId, ""), r.Procedure, r.StartsOn, r.Status.DisplayName, r.Outcome?.DisplayName,
                SurgeryReadFormat.Money(r.Amount, r.Currency), standard is null ? null : SurgeryReadFormat.Money(standard.Amount, standard.CurrencyCode), r.CampaignEligibilityNote);
        }).ToList();

        var counted = forTotals.Where(r => r.Status != SurgeryStatus.Cancelled).ToList();
        var currency = counted.Select(r => r.Currency).FirstOrDefault() ?? forTotals.Select(r => r.Currency).FirstOrDefault() ?? "BDT";
        var campaignTotal = counted.Sum(r => r.Amount);
        var standardTotal = counted.Sum(r => standardAt(r.ClinicServiceId, r.CreatedDate)?.Amount ?? 0m);

        return new QueryResponse<CampaignCasesViewModel?>
        {
            Result = new CampaignCasesViewModel(
                campaign.Id, campaign.Code, campaign.Name, cases,
                new CampaignCaseTotalsViewModel(counted.Count, SurgeryReadFormat.Money(campaignTotal, currency), SurgeryReadFormat.Money(standardTotal, currency)),
                query.PageNumber, query.PageSize, forTotals.Count)
        };
    }
}
