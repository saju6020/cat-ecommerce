using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Administration;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Administration;

/// <summary>Current price via a "greatest EffectiveFrom &lt;= now" correlated lookup in SQL
/// (§12.6) -- never re-implemented client-side.</summary>
public sealed class GetPublicServicesQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetPublicServicesQuery, IReadOnlyList<PublicServiceViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<PublicServiceViewModel>>> HandleAsync(GetPublicServicesQuery query)
    {
        var now = timeProvider.GetUtcNow();

        var services = await db.Set<ClinicServiceAggregate>().AsNoTracking()
            .Where(s => s.IsActive && s.IsBookableOnline)
            .Select(s => new
            {
                s.Id, s.Code, s.Name, s.Description, s.Category, s.DurationMinutes, s.IsBookableOnline,
                Price = db.Set<ServicePriceAggregate>()
                    .Where(p => p.ClinicServiceId == s.Id && p.EffectiveFrom <= now)
                    .OrderByDescending(p => p.EffectiveFrom)
                    .Select(p => new { p.Amount.Amount, p.Amount.CurrencyCode })
                    .FirstOrDefault()
            })
            .ToListAsync();

        var result = services
            .Where(s => s.Price is not null)
            .Select(s => new PublicServiceViewModel(
                s.Id, s.Code, s.Name, s.Description, s.Category.DisplayName, s.DurationMinutes, s.IsBookableOnline,
                new MoneyViewModel(s.Price!.Amount.ToString("F2"), s.Price.CurrencyCode)))
            .ToList();

        return new QueryResponse<IReadOnlyList<PublicServiceViewModel>> { Result = result };
    }
}

public sealed class GetAdminServicesQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetAdminServicesQuery, IReadOnlyList<AdminServiceViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<AdminServiceViewModel>>> HandleAsync(GetAdminServicesQuery query)
    {
        var now = timeProvider.GetUtcNow();

        // Gate fix 2026-09-26: previously materialised every ClinicServiceAggregate, then ran one
        // extra AnyAsync round trip per service (N+1) to compute isPriced. A single Select
        // projection, with isPriced as a correlated subquery, replaces both.
        var rows = await db.Set<ClinicServiceAggregate>().AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new
            {
                s.Id, s.Code, s.Name, s.Description, s.Category, s.DurationMinutes, s.QualifiedRoleIds,
                s.RequiredRoomKind, s.TaxClassId, s.IsBookableOnline, s.IsActive,
                IsPriced = db.Set<ServicePriceAggregate>().Any(p => p.ClinicServiceId == s.Id && p.EffectiveFrom <= now)
            })
            .ToListAsync();

        var result = rows.Select(s => new AdminServiceViewModel(
            s.Id, s.Code, s.Name, s.Description, s.Category.DisplayName, s.DurationMinutes, s.QualifiedRoleIds,
            s.RequiredRoomKind.DisplayName, s.TaxClassId, s.IsBookableOnline, s.IsActive, s.IsPriced)).ToList();

        return new QueryResponse<IReadOnlyList<AdminServiceViewModel>> { Result = result };
    }
}

public sealed class GetAdminServiceQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider)
    : IQueryHandlerAsync<GetAdminServiceQuery, AdminServiceViewModel?>
{
    public async Task<QueryResponse<AdminServiceViewModel?>> HandleAsync(GetAdminServiceQuery query)
    {
        var now = timeProvider.GetUtcNow();

        var row = await db.Set<ClinicServiceAggregate>().AsNoTracking()
            .Where(s => s.Id == query.ServiceId)
            .Select(s => new
            {
                s.Id, s.Code, s.Name, s.Description, s.Category, s.DurationMinutes, s.QualifiedRoleIds,
                s.RequiredRoomKind, s.TaxClassId, s.IsBookableOnline, s.IsActive,
                IsPriced = db.Set<ServicePriceAggregate>().Any(p => p.ClinicServiceId == s.Id && p.EffectiveFrom <= now)
            })
            .FirstOrDefaultAsync();

        if (row is null) return new QueryResponse<AdminServiceViewModel?> { Result = null };

        var result = new AdminServiceViewModel(
            row.Id, row.Code, row.Name, row.Description, row.Category.DisplayName, row.DurationMinutes, row.QualifiedRoleIds,
            row.RequiredRoomKind.DisplayName, row.TaxClassId, row.IsBookableOnline, row.IsActive, row.IsPriced);

        return new QueryResponse<AdminServiceViewModel?> { Result = result };
    }
}

public sealed class GetServicePricesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetServicePricesQuery, IReadOnlyList<ServicePriceViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<ServicePriceViewModel>>> HandleAsync(GetServicePricesQuery query)
    {
        var rows = await db.Set<ServicePriceAggregate>().AsNoTracking()
            .Where(p => p.ClinicServiceId == query.ServiceId)
            .OrderByDescending(p => p.EffectiveFrom)
            .Take(AdministrationListCaps.MaxUnboundedListSize)
            .Select(p => new { p.Id, p.ClinicServiceId, p.Amount.Amount, p.Amount.CurrencyCode, p.EffectiveFrom, p.SetBy })
            .ToListAsync();

        // Money.Amount.ToString("F2") is formatting, not a database concern -- it cannot be
        // translated to SQL, so it stays in this final, already-narrow in-memory projection.
        var result = rows.Select(p => new ServicePriceViewModel(
            p.Id, p.ClinicServiceId, new MoneyViewModel(p.Amount.ToString("F2"), p.CurrencyCode), p.EffectiveFrom, p.SetBy)).ToList();

        return new QueryResponse<IReadOnlyList<ServicePriceViewModel>> { Result = result };
    }
}

public sealed class GetTaxClassesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetTaxClassesQuery, IReadOnlyList<TaxClassViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<TaxClassViewModel>>> HandleAsync(GetTaxClassesQuery query)
    {
        var result = await db.Set<TaxClassAggregate>().AsNoTracking().OrderBy(t => t.Name)
            .Select(t => new TaxClassViewModel(t.Id, t.Code, t.Name, t.IsActive))
            .ToListAsync();

        return new QueryResponse<IReadOnlyList<TaxClassViewModel>> { Result = result };
    }
}

public sealed class GetTaxRatesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetTaxRatesQuery, IReadOnlyList<TaxRateViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<TaxRateViewModel>>> HandleAsync(GetTaxRatesQuery query)
    {
        var result = await db.Set<TaxRateAggregate>().AsNoTracking()
            .Where(r => r.TaxClassId == query.TaxClassId)
            .OrderByDescending(r => r.EffectiveFrom)
            .Take(AdministrationListCaps.MaxUnboundedListSize)
            .Select(r => new TaxRateViewModel(r.Id, r.TaxClassId, r.RatePercent, r.EffectiveFrom))
            .ToListAsync();

        return new QueryResponse<IReadOnlyList<TaxRateViewModel>> { Result = result };
    }
}
