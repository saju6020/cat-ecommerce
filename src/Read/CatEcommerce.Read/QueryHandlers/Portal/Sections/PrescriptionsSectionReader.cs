using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Portal.Sections;

/// <summary>
/// HO-PHM-001 (HO-POR-001 §8.3, §8.6): how many active prescriptions each cat has and when the soonest course ends -- nothing else.
/// Medicine, dose, route, frequency, repeat state and the issuing vet are read and discarded (ADR-262); the output type has no property
/// that could hold them.
///
/// Reconciled with the real contract (HO-POR-001 §1 Amended 2026-10-10): Pharmacy publishes <c>GetCustomerPrescriptionsQuery
/// { CustomerId, Scope = "active", CatId? }</c>; the course end is <c>CourseEndsOn</c>. Pharmacy reaches each prescription through its
/// cat's CURRENT owner, so the list is the caller's own by construction (ADR-261). Rows Pharmacy flags inactive are dropped again here
/// (defence in depth). Pharmacy does not filter by cat life status and nothing cancels a prescription when a cat is marked deceased, so rows are
/// also kept only for cats in the caller's alive-cat list (the owner-filtered Cat Profiles call, read in full -- every page, not the dashboard's 12-cat page; SECURITY L1,
/// POR backlog 8 and 17) -- a deceased cat never appears on the dashboard.
/// </summary>
public sealed class PrescriptionsSectionReader(
    IQueryHandlerAsync<GetCatsQuery, PagedResult<CatListItemViewModel>> catsHandler,
    IQueryHandlerAsync<GetCustomerPrescriptionsQuery, IReadOnlyList<CustomerPrescriptionViewModel>> prescriptionsHandler)
    : IPortalSectionReader<PortalPrescriptionsViewModel>
{
    public async Task<PortalPrescriptionsViewModel> ReadAsync(PortalCaller caller)
    {
        var aliveCatIds = await ReadAllAliveCatIdsAsync(caller);

        var rows = (await prescriptionsHandler.HandleAsync(new GetCustomerPrescriptionsQuery
        {
            CustomerId = caller.CustomerId, Scope = "active", UserContext = caller.UserContext, CorrelationId = caller.CorrelationId,
        })).Result;

        var byCat = rows
            .Where(p => p.IsActive && aliveCatIds.Contains(p.CatId))
            .GroupBy(p => p.CatId)
            .Select(g => new PortalPrescriptionCatViewModel(g.Key, g.First().CatName, g.Count(), g.Min(p => p.CourseEndsOn)))
            .OrderBy(c => c.EarliestEndsOn).ThenBy(c => c.CatName, StringComparer.Ordinal)
            .ToList();

        return new PortalPrescriptionsViewModel(byCat);
    }

    /// <summary>
    /// Every alive cat of the caller, paged through Cat Profiles' published <c>GetCatsQuery</c> (it has no ids-only option; the page cap is
    /// <see cref="PagingRules.MaxPageSize"/>). The dashboard's own 12-cat limit must not apply here: it would silently drop the prescription rows
    /// of a customer's 13th and later cats (POR backlog 17).
    /// </summary>
    private async Task<HashSet<Guid>> ReadAllAliveCatIdsAsync(PortalCaller caller)
    {
        var ids = new HashSet<Guid>();
        for (var pageNumber = 1; ; pageNumber++)
        {
            var page = (await catsHandler.HandleAsync(new GetCatsQuery
            {
                OwnerId = caller.CustomerId,
                IncludeDeceased = false,
                PageNumber = pageNumber,
                PageSize = PagingRules.MaxPageSize,
                UserContext = caller.UserContext,
                CorrelationId = caller.CorrelationId,
            })).Result;

            foreach (var cat in page.Items) ids.Add(cat.Id);
            if (page.Items.Count == 0 || ids.Count >= page.TotalCount) return ids;
        }
    }
}
