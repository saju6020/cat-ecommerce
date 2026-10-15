using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Prescription;

internal sealed record DietRow(
    Guid Id, Guid CatId, Guid ProductId, string ProductName, DateOnly ValidFrom, DateOnly ValidTo, DietAuthorisationStatus Status, string AuthorisedBy, Guid VisitId, string? Notes);

internal static class DietAuthorisationReads
{
    public static IQueryable<DietRow> Rows(CatEcommerceDbContext db, IQueryable<DietAuthorisationAggregate> source) =>
        from a in source.AsNoTracking()
        join s in db.StaffAccounts.AsNoTracking() on a.AuthorisedBy equals s.Id into staff
        from s in staff.DefaultIfEmpty()
        select new DietRow(a.Id, a.CatId, a.ProductId, a.ProductNameSnapshot, a.ValidFrom, a.ValidTo, a.Status, s != null ? s.FullName : "", a.VisitId, a.Notes);

    /// <summary>Stored Active but past its last day reads Expired -- the product is locked by the gate whether or not the sweep has run.</summary>
    public static DietAuthorisationStatus Effective(DietRow r, DateOnly today) =>
        r.Status == DietAuthorisationStatus.Active && today > r.ValidTo ? DietAuthorisationStatus.Expired : r.Status;

    public static DietAuthorisationViewModel ToView(DietRow r, DateOnly today) =>
        new(r.Id, r.CatId, r.ProductId, r.ProductName, r.ValidFrom, r.ValidTo, Effective(r, today).DisplayName, r.AuthorisedBy, r.VisitId, r.Notes);
}

public sealed class GetCatDietAuthorisationsQueryHandler(CatEcommerceDbContext db, IPharmacyClock clock)
    : IQueryHandlerAsync<GetCatDietAuthorisationsQuery, IReadOnlyList<DietAuthorisationViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<DietAuthorisationViewModel>>> HandleAsync(GetCatDietAuthorisationsQuery query)
    {
        var today = await clock.MainBranchTodayAsync(CancellationToken.None);
        var catId = query.CatId;
        var rows = await DietAuthorisationReads.Rows(db, db.DietAuthorisations.Where(a => a.CatId == catId && !a.IsMarkedToDelete)).ToListAsync();

        var wantAll = string.Equals(query.Scope, "all", StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<DietAuthorisationViewModel> result = rows
            .Where(r => wantAll || DietAuthorisationReads.Effective(r, today) == DietAuthorisationStatus.Active)
            .OrderByDescending(r => r.ValidTo).ThenBy(r => r.Id).Select(r => DietAuthorisationReads.ToView(r, today)).ToList();
        return new QueryResponse<IReadOnlyList<DietAuthorisationViewModel>> { Result = result };
    }
}

public sealed class GetVisitDietAuthorisationsQueryHandler(CatEcommerceDbContext db, IPharmacyClock clock)
    : IQueryHandlerAsync<GetVisitDietAuthorisationsQuery, IReadOnlyList<DietAuthorisationViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<DietAuthorisationViewModel>>> HandleAsync(GetVisitDietAuthorisationsQuery query)
    {
        var today = await clock.MainBranchTodayAsync(CancellationToken.None);
        var visitId = query.VisitId;
        var rows = await DietAuthorisationReads.Rows(db, db.DietAuthorisations.Where(a => a.VisitId == visitId && !a.IsMarkedToDelete)).ToListAsync();

        IReadOnlyList<DietAuthorisationViewModel> result = rows.OrderBy(r => r.ProductName).ThenBy(r => r.Id).Select(r => DietAuthorisationReads.ToView(r, today)).ToList();
        return new QueryResponse<IReadOnlyList<DietAuthorisationViewModel>> { Result = result };
    }
}

public sealed class SearchDietProductsQueryHandler(ICatalogueReader catalogue)
    : IQueryHandlerAsync<SearchDietProductsQuery, IReadOnlyList<DietProductViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<DietProductViewModel>>> HandleAsync(SearchDietProductsQuery query)
    {
        var products = await catalogue.SearchPrescriptionProductsAsync(query.Search, 50, CancellationToken.None);
        IReadOnlyList<DietProductViewModel> result = products.Select(p => new DietProductViewModel(p.ProductId, p.Name, p.Brand)).ToList();
        return new QueryResponse<IReadOnlyList<DietProductViewModel>> { Result = result };
    }
}

public sealed class GetCustomerDietAuthorisationsQueryHandler(CatEcommerceDbContext db, IPharmacyClock clock)
    : IQueryHandlerAsync<GetCustomerDietAuthorisationsQuery, IReadOnlyList<CustomerDietAuthorisationViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<CustomerDietAuthorisationViewModel>>> HandleAsync(GetCustomerDietAuthorisationsQuery query)
    {
        var ct = CancellationToken.None;
        var today = await clock.MainBranchTodayAsync(ct);
        var customerId = query.CustomerId;

        var source = db.DietAuthorisations.Where(a => !a.IsMarkedToDelete && db.Cats.Any(c => c.Id == a.CatId && c.OwnerId == customerId));
        if (query.CatId is { } catId) source = source.Where(a => a.CatId == catId);

        var rows = await DietAuthorisationReads.Rows(db, source).ToListAsync(ct);
        var wantActive = !string.Equals(query.Scope, "past", StringComparison.OrdinalIgnoreCase);
        var chosen = rows.Where(r => (DietAuthorisationReads.Effective(r, today) == DietAuthorisationStatus.Active) == wantActive)
            .OrderByDescending(r => r.ValidTo).ThenBy(r => r.Id).Take(200).ToList();

        var catNames = await ControlledDrugReads.CatNamesAsync(db, chosen.Select(r => (Guid?)r.CatId), ct);
        IReadOnlyList<CustomerDietAuthorisationViewModel> result = chosen.Select(r => new CustomerDietAuthorisationViewModel(
            r.Id, r.CatId, catNames.GetValueOrDefault(r.CatId) ?? "", r.ProductId, r.ProductName, r.ValidFrom, r.ValidTo, DietAuthorisationReads.Effective(r, today).DisplayName, r.AuthorisedBy)).ToList();
        return new QueryResponse<IReadOnlyList<CustomerDietAuthorisationViewModel>> { Result = result };
    }
}

public sealed class GetExpirableDietAuthorisationsQueryHandler(CatEcommerceDbContext db, IPharmacyClock clock)
    : IQueryHandlerAsync<GetExpirableDietAuthorisationsQuery, IReadOnlyList<Guid>>
{
    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetExpirableDietAuthorisationsQuery query)
    {
        var today = await clock.MainBranchTodayAsync(CancellationToken.None);
        var take = Math.Clamp(query.Limit, 1, 200);
        IReadOnlyList<Guid> ids = await db.DietAuthorisations.AsNoTracking()
            .Where(a => !a.IsMarkedToDelete && a.Status == DietAuthorisationStatus.Active && a.ValidTo < today)
            .OrderBy(a => a.ValidTo).ThenBy(a => a.Id).Select(a => a.Id).Take(take).ToListAsync();
        return new QueryResponse<IReadOnlyList<Guid>> { Result = ids };
    }
}
