using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Cat;

/// <summary>
/// Q4 -- GET /api/cats/{catId}/vaccination-status. US-CAT-07, FR-CAT-7. Computed entirely
/// read-side; no aggregate is rehydrated. The algorithm itself lives in
/// CatVaccinationStatusCalculator so it can be unit-tested without a database.
///
/// HO-ADM-001 §12.2 rewire: reads <c>clinical.vaccinationDueSoonDays</c> from
/// <see cref="IBusinessSettings"/> instead of <c>IOptions&lt;CatProfileOptions&gt;</c> -- the
/// registry's default (30) is seeded from the same value <c>CatProfileOptions</c> used to hard-code
/// (§10.2). Nothing else in this file changes (F20).
/// </summary>
public sealed class GetCatVaccinationStatusQueryHandler(
    ILogger<GetCatVaccinationStatusQueryHandler> logger,
    CatEcommerceDbContext db,
    IBusinessSettings settings)
    : IQueryHandlerAsync<GetCatVaccinationStatusQuery, CatVaccinationStatusViewModel?>
{
    public async Task<QueryResponse<CatVaccinationStatusViewModel?>> HandleAsync(
        GetCatVaccinationStatusQuery query)
    {
        logger.LogInformation("Reading vaccination status for cat {CatId}", query.CatId);

        var catExists = await db.Cats.AsNoTracking().AnyAsync(c => c.Id == query.CatId);
        if (!catExists)
            return new QueryResponse<CatVaccinationStatusViewModel?> { Result = null };

        var activeCoreVaccines = await db.Vaccines.AsNoTracking()
            .Where(v => v.IsCore && v.IsActive)
            .Select(v => new ActiveCoreVaccine(v.Id, v.Code, v.Name))
            .ToListAsync();

        var catVaccinations = await db.Cats.AsNoTracking()
            .Where(c => c.Id == query.CatId)
            .SelectMany(c => c.Vaccinations)
            .Select(v => new CatVaccinationRecord(v.VaccineId, v.AdministeredOn, v.NextDueOn))
            .ToListAsync();

        var dueSoonWindowDays = settings.GetInt(SettingKeys.ClinicalVaccinationDueSoonDays);

        var result = CatVaccinationStatusCalculator.Calculate(
            query.CatId, activeCoreVaccines, catVaccinations,
            DateOnly.FromDateTime(DateTime.UtcNow), dueSoonWindowDays);

        return new QueryResponse<CatVaccinationStatusViewModel?> { Result = result };
    }
}
