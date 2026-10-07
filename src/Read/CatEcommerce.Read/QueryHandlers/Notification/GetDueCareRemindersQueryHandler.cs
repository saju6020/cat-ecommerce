using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Notification;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Notification;

/// <summary>HO-NTF-001 §8.12. Alive cats with an active owner; latest vaccination per active
/// vaccine and latest deworming/combined treatment, each checked against its own lead-day window.</summary>
public sealed class GetDueCareRemindersQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetDueCareRemindersQuery, IReadOnlyList<DueCareReminderRow>>
{
    public async Task<QueryResponse<IReadOnlyList<DueCareReminderRow>>> HandleAsync(GetDueCareRemindersQuery query)
    {
        var activeVaccineIds = await db.Vaccines.AsNoTracking().Where(v => v.IsActive).Select(v => v.Id).ToListAsync();
        var activeOwnerIds = (await db.CustomerAccounts.AsNoTracking()
            .Where(c => c.Status == CustomerAccountStatus.Active)
            .Select(c => c.Id)
            .ToListAsync()).ToHashSet();

        var cats = await db.Cats.AsNoTracking()
            .Where(c => c.LifeStatus == CatLifeStatus.Alive)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.OwnerId,
                c.OwnershipGeneration,
                Vaccinations = c.Vaccinations.Select(v => new { v.VaccineId, v.VaccineName, v.AdministeredOn, v.NextDueOn }).ToList(),
                ParasiteTreatments = c.ParasiteTreatments.Select(p => new { p.TreatmentType, p.ProductName, p.AdministeredOn, p.NextDueOn }).ToList()
            })
            .ToListAsync();

        var rows = new List<DueCareReminderRow>();

        foreach (var cat in cats)
        {
            if (!activeOwnerIds.Contains(cat.OwnerId)) continue;

            var latestPerVaccine = cat.Vaccinations
                .Where(v => activeVaccineIds.Contains(v.VaccineId))
                .GroupBy(v => v.VaccineId)
                .Select(g => g.OrderByDescending(v => v.AdministeredOn).First());

            foreach (var vaccination in latestPerVaccine)
            {
                if (IsDue(vaccination.NextDueOn, query.Today, query.VaccinationLeadDays))
                    rows.Add(new DueCareReminderRow(cat.Id, cat.Name, cat.OwnerId, "Vaccination", vaccination.VaccineId, vaccination.VaccineName, vaccination.NextDueOn, cat.OwnershipGeneration));
            }

            var latestDeworming = cat.ParasiteTreatments
                .Where(t => t.TreatmentType == ParasiteTreatmentType.Deworming || t.TreatmentType == ParasiteTreatmentType.Combined)
                .OrderByDescending(t => t.AdministeredOn)
                .FirstOrDefault();

            if (latestDeworming is not null && IsDue(latestDeworming.NextDueOn, query.Today, query.DewormingLeadDays))
                rows.Add(new DueCareReminderRow(cat.Id, cat.Name, cat.OwnerId, "Deworming", Guid.Empty, latestDeworming.ProductName, latestDeworming.NextDueOn, cat.OwnershipGeneration));
        }

        return new QueryResponse<IReadOnlyList<DueCareReminderRow>> { Result = rows };
    }

    private static bool IsDue(DateOnly dueOn, DateOnly today, int leadDays) => dueOn.AddDays(-leadDays) <= today && today <= dueOn;
}
